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

    public async Task<OrderResponse?> CreateAsync (CreateOrderRequest request, int customerId)
    {
        // 1. Ensure the customer exists.
        var customerExists = await _context.Customers
            .AnyAsync(c => c.Id == customerId);

        if (!customerExists)
            return null;

        if (request.Items.Count == 0)
            throw new InvalidOrderException(
                "An order must contain at least one item.");

        // Keep one line per product in an order.
        // Select extracts product ids in this case. Distinct() removes duplicates
        var productIds = request.Items
            .Select(i => i.ProductId)
            .ToList();

        if (productIds.Distinct().Count() != productIds.Count) // if the number of distinct ids < original count, duplicate product ids were supplied. 
            throw new InvalidOrderException(
                "A product can only appear once per order.");

        // 2. Load trusted product data from the database.
        var products = await _context.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        // Reject the entire order if any product doesn't exist.
        if (products.Count != productIds.Count)
            throw new InvalidOrderException(
                "One or more product IDs are invalid.");

        // 3. Construct the order and snapshot each current product price.
        var order = new Order
        {
            CustomerId = customerId,
            Status = OrderStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            OrderItems = request.Items.Select(item =>
            {
                var product = products[item.ProductId];

                if (item.Quantity <= 0)
                    throw new InvalidOrderException("Quantity must be greater than zero.");

                return new OrderItem
                {
                    ProductId = product.Id,
                    Product = product,
                    Quantity = item.Quantity,
                    UnitPrice = product.Price
                };
            }).ToList()
        };

        _context.Orders.Add(order);

        // A single SaveChanges call persists the order and its items.
        await _context.SaveChangesAsync();

        return MapToResponse(order);
    }

    public async Task<OrderResponse?> GetByIdAsync(int id, int? customerId)
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

        var order = await query.FirstOrDefaultAsync(o => o.Id == id);

        return order is null ? null : MapToResponse(order);
    }

    public async Task<(IEnumerable<OrderResponse> Items, int TotalCount)> GetAllAsync(int page, int pageSize, int? customerId)
    {
        var query = _context.Orders
            .AsNoTracking()
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
            .AsQueryable();

            if(customerId.HasValue)
        {
            query = query.Where(o => o.CustomerId == customerId.Value);
        }

        var totalCount = await query.CountAsync();

        var orders = await query
            .OrderByDescending(o => o.CreatedAt)
            .ThenByDescending(o => o.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

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