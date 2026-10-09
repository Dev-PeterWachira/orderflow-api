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

    public async Task<OrderResponse?> CreateAsync(
        CreateOrderRequest request)
    {
        // 1. Ensure the customer exists.
        var customerExists = await _context.Customers
            .AnyAsync(c => c.Id == request.CustomerId);

        if (!customerExists)
            return null;

        if (request.Items.Count == 0)
            throw new InvalidOrderException(
                "An order must contain at least one item.");

        // Keep one line per product in an order.
        var productIds = request.Items
            .Select(i => i.ProductId)
            .ToList();

        if (productIds.Distinct().Count() != productIds.Count)
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
            CustomerId = request.CustomerId,
            Status = OrderStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            OrderItems = request.Items.Select(item =>
            {
                var product = products[item.ProductId];

                if (item.Quantity <= 0)
                    throw new InvalidOrderException(
                        "Quantity must be greater than zero.");

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

    public async Task<OrderResponse?> GetByIdAsync(int id)
    {
        var order = await _context.Orders
            .AsNoTracking()
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
            .FirstOrDefaultAsync(o => o.Id == id);

        return order is null ? null : MapToResponse(order);
    }

    public async Task<(IEnumerable<OrderResponse> Items, int TotalCount)>
        GetAllAsync(int page, int pageSize)
    {
        var query = _context.Orders
            .AsNoTracking()
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product);

        var totalCount = await _context.Orders.CountAsync();

        var orders = await query
            .OrderByDescending(o => o.CreatedAt)
            .ThenByDescending(o => o.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (orders.Select(MapToResponse), totalCount);
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