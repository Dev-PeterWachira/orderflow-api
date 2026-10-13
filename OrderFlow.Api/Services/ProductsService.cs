using Microsoft.EntityFrameworkCore;
using OrderFlow.Api.Data;
using OrderFlow.Api.DTOs.Products;
using OrderFlow.Api.Models;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace OrderFlow.Api.Services;

public class ProductService : IProductService
{
  private readonly OrderFlowDbContext _context;
private readonly IHttpContextAccessor _httpContextAccessor;

public ProductService(OrderFlowDbContext context,IHttpContextAccessor httpContextAccessor)
{
    _context = context;
    _httpContextAccessor = httpContextAccessor;
}

    public async Task<ProductResponse> CreateAsync(ProductCreateRequest request)
    {
        var product = new Product
        {
            Name = request.Name,
            Price = request.Price,
            StockQuantity = request.StockQuantity
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        return MapToResponse(product);
    }

    public async Task<ProductResponse?> GetByIdAsync(int id)
    {
        var product = await _context.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);

        return product is null ? null : MapToResponse(product);
    }

    public async Task<(IEnumerable<ProductResponse> Items, int TotalCount)>
        GetAllAsync(string? search, int page, int pageSize)
    {
        var query = _context.Products
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(p => p.Name.Contains(search));
        }

        var totalCount = await query.CountAsync();

        var products = await query
            .OrderBy(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = products.Select(MapToResponse);

        return (items, totalCount);
    }

    public async Task<ProductResponse?> UpdateAsync(int id,ProductUpdateRequest request)
{
    var product = await _context.Products
        .FirstOrDefaultAsync(p => p.Id == id);

    if (product is null)
        return null;

    var oldPrice = product.Price;
    var oldStockQuantity = product.StockQuantity;

    var priceChanged = oldPrice != request.Price;
    var stockChanged = oldStockQuantity != request.StockQuantity;

    product.Name = request.Name;
    product.Price = request.Price;
    product.StockQuantity = request.StockQuantity;

    if (priceChanged || stockChanged)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        var actorId = user?.FindFirstValue(ClaimTypes.NameIdentifier);
        var actorRole = user?.FindFirstValue(ClaimTypes.Role);

        if (string.IsNullOrWhiteSpace(actorId) ||
            string.IsNullOrWhiteSpace(actorRole))
        {
            throw new InvalidOperationException(
                "Cannot audit a product change without an authenticated actor.");
        }

        if (priceChanged)
        {
            _context.AuditLogs.Add(new AuditLog
            {
                ActorId = actorId,
                ActorRole = actorRole,
                Action = "ProductPriceChanged",
                EntityType = "Product",
                EntityId = product.Id,
                TimestampUtc = DateTime.UtcNow,
                Details = $"Price changed from {oldPrice} to {request.Price}."
            });
        }

        if (stockChanged)
        {
            _context.AuditLogs.Add(new AuditLog
            {
                ActorId = actorId,
                ActorRole = actorRole,
                Action = "ProductStockChanged",
                EntityType = "Product",
                EntityId = product.Id,
                TimestampUtc = DateTime.UtcNow,
                Details = $"Stock changed from {oldStockQuantity} to {request.StockQuantity}."
            });
        }
    }

    // Product changes and audit records are persisted together.
    await _context.SaveChangesAsync();

    return MapToResponse(product);
}

    public async Task<bool> DeleteAsync(int id)
    {
        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product is null)
            return false;

        _context.Products.Remove(product);
        await _context.SaveChangesAsync();

        return true;
    }

    private static ProductResponse MapToResponse(Product product)
    {
        return new ProductResponse
        {
            Id = product.Id,
            Name = product.Name,
            Price = product.Price,
            StockQuantity = product.StockQuantity
        };
    }
}
