using Microsoft.EntityFrameworkCore;
using OrderFlow.Api.Data;
using OrderFlow.Api.DTOs.Orders;
using OrderFlow.Api.Models;

namespace OrderFlow.Api.Services;

public class OrderService : IOrderService
{
    private readonly OrderFlowDbContext _context;

    public OrderService(OrderFlowDbContext context)
    {
        _context = context;
    }

    
public async Task<OrderResponse?> CreateAsync(CreateOrderRequest request,int customerId, CancellationToken cancellationToken = default)
{
    // 1. Ensure the authenticated customer exists.
    var customerExists = await _context.Customers
        .AnyAsync(c => c.Id == customerId,cancellationToken);

    if (!customerExists)
        return null;

    // 2. Validate the order contains items.
    if (request.Items.Count == 0)
    {
        throw new InvalidOrderException(
            "An order must contain at least one item.");
    }

    // 3. Ensure each product appears only once.
    var productIds = request.Items
        .Select(item => item.ProductId)
        .ToList();

    if (productIds.Distinct().Count() != productIds.Count)
    {
        throw new InvalidOrderException(
            "A product can only appear once per order.");
    }

    // 4. Load trusted product data from the database.
    var products = await _context.Products
        .Where(product => productIds.Contains(product.Id))
        .ToDictionaryAsync(product => product.Id, cancellationToken);

    if (products.Count != productIds.Count)
    {
        throw new InvalidOrderException(
            "One or more product IDs are invalid.");
    }

    // 5. Validate quantities and construct the order.
    var orderItems = request.Items.Select(item =>
    {
        if (item.Quantity <= 0)
        {
            throw new InvalidOrderException(
                "Quantity must be greater than zero.");
        }

        var product = products[item.ProductId];

        return new OrderItem
        {
            ProductId = product.Id,
            Product = product,
            Quantity = item.Quantity,
            UnitPrice = product.Price
        };
    }).ToList();

    var order = new Order
    {
        CustomerId = customerId,
        Status = OrderStatus.Pending,
        CreatedAt = DateTime.UtcNow,
        OrderItems = orderItems
    };

    // 6. Persist the order and audit entry atomically.
    await using var transaction =
        await _context.Database.BeginTransactionAsync(cancellationToken);

    try
    {
        _context.Orders.Add(order);

        // First save obtains the database-generated order ID.
        await _context.SaveChangesAsync(cancellationToken);

        _context.AuditLogs.Add(new AuditLog
        {
            ActorId = customerId.ToString(),
            ActorRole = "Customer",
            Action = "OrderCreated",
            EntityType = "Order",
            EntityId = order.Id,
            TimestampUtc = DateTime.UtcNow,
            Details =
                $"Order created with {order.OrderItems.Count} item(s)."
        });

        // Persist the audit record in the same transaction.
        await _context.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return MapToResponse(order);
    }
    catch
    {
        await transaction.RollbackAsync(cancellationToken);
        throw;
    }
}

    public async Task<OrderResponse?> GetByIdAsync(int id, int? customerId, CancellationToken cancellationToken = default)
    {
        var query =  _context.Orders
            .AsNoTracking()
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
            .AsQueryable();

        if (customerId.HasValue)
        {
            query = query.Where(o => o.CustomerId == customerId.Value);
        }

        var order = await query.FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        return order is null ? null : MapToResponse(order);
    }

    public async Task<(IEnumerable<OrderResponse> Items, int TotalCount)> GetAllAsync(int page, int pageSize, int? customerId, CancellationToken cancellationToken = default)
    {
        var query = _context.Orders
            .AsNoTracking()
            .AsSplitQuery()
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
            .AsQueryable();

            if(customerId.HasValue)
        {
            query = query.Where(o => o.CustomerId == customerId.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var orders = await query
            .OrderByDescending(o => o.CreatedAt)
            .ThenByDescending(o => o.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (orders.Select(MapToResponse), totalCount);
    }

    
public async Task<OrderResponse?> ProcessAsync (int orderId,CancellationToken cancellationToken = default)
{
    await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

    try
    {
        // 1. Atomically claim the order.
        // Only one request can change Pending to Processing.
        var claimed = await _context.Orders
            .Where(o =>
                o.Id == orderId &&
                o.Status == OrderStatus.Pending)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    o => o.Status,
                    OrderStatus.Processing),
                cancellationToken);

        if (claimed == 0)
        {
            var exists = await _context.Orders
                .AnyAsync(o => o.Id == orderId, cancellationToken);

            if (!exists)
                return null;

            throw new InvalidOrderException(
                "This order has already been processed or cannot be processed.");
        }

        // 2. Load the order and its items.
        var order = await _context.Orders
            .Include(o => o.OrderItems)
                .ThenInclude(item => item.Product)
            .SingleAsync(o => o.Id == orderId, cancellationToken);

        if (order.OrderItems.Count == 0)
            throw new InvalidOrderException(
                "An order must contain at least one item.");

        // Use the price snapshot saved when the order was created.
        var total = order.OrderItems.Sum(
            item => item.UnitPrice * item.Quantity);

        // 3. Reduce stock using conditional database updates.
        foreach (var item in order.OrderItems)
        {
            if (item.Quantity <= 0)
                throw new InvalidOrderException(
                    "Order item quantities must be greater than zero.");

            var updated = await _context.Products
                .Where(p =>
                    p.Id == item.ProductId &&
                    p.StockQuantity >= item.Quantity)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        p => p.StockQuantity,
                        p => p.StockQuantity - item.Quantity),
                    cancellationToken);

            if (updated != 1)
            {
                throw new InvalidOrderException(
                    $"Insufficient stock for product {item.ProductId}.");
            }
        }

        // 4. Record payment and complete the order.
        _context.Payments.Add(new Payment
        {
            OrderId = order.Id,
            Amount = total,
            CreatedAt = DateTime.UtcNow
        });

        order.Status = OrderStatus.Completed;

        await _context.SaveChangesAsync(cancellationToken);

        // 5. Commit all changes together.
        await transaction.CommitAsync(cancellationToken);

        return MapToResponse(order);
    }
    catch
    {
        await transaction.RollbackAsync(cancellationToken);
        throw;
    }
}
    public async Task<OrderResponse?> CancelAsync(
    int orderId,
    int customerId,
    CancellationToken cancellationToken = default)
{
    await using var transaction =
        await _context.Database.BeginTransactionAsync(cancellationToken);

    try
    {
        // Only the owning customer can cancel a Pending order.
        var claimed = await _context.Orders
            .Where(o =>
                o.Id == orderId &&
                o.CustomerId == customerId &&
                o.Status == OrderStatus.Pending)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    o => o.Status,
                    OrderStatus.Cancelled),
                cancellationToken);

        if (claimed == 0)
        {
            var orderExists = await _context.Orders
                .AnyAsync(o => o.Id == orderId, cancellationToken);

            if (!orderExists)
                return null;

            throw new InvalidOrderException(
                "The order cannot be cancelled. It may not belong to you, " +
                "or it may already have been processed.");
        }

        var order = await _context.Orders
            .Include(o => o.OrderItems)
                .ThenInclude(item => item.Product)
            .SingleAsync(o => o.Id == orderId, cancellationToken);

        // Record the cancellation in the same transaction.
        _context.AuditLogs.Add(new AuditLog
        {
            ActorId = customerId.ToString(),
            ActorRole = "Customer",
            Action = "OrderCancelled",
            EntityType = "Order",
            EntityId = order.Id,
            TimestampUtc = DateTime.UtcNow,
            Details = $"Order {order.Id} was cancelled."
        });

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return MapToResponse(order);
    }
    catch
    {
        await transaction.RollbackAsync(cancellationToken);
        throw;
    }
}


    private static OrderResponse MapToResponse(Order order)
    {
        var items = order.OrderItems.Select(item =>
            new OrderItemResponse
            {
                ProductId = item.ProductId,
                ProductName = item.Product.Name,
                UnitPrice = item.UnitPrice,
                Quantity = item.Quantity
            }).ToList();

        return new OrderResponse
        {
            Id = order.Id,
            CustomerId = order.CustomerId,
            Status = order.Status,
            CreatedAt = order.CreatedAt,
            Items = items,
            Total = items.Sum(item => item.LineTotal)
        };
    }
}