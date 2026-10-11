
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Api.Data;
using OrderFlow.Api.DTOs.Orders;
using OrderFlow.Api.Models;
using OrderFlow.Api.Services;

namespace OrderFlow.Api.Tests;

public class OrderServiceTests
{
    private static async Task<OrderFlowDbContext> CreateContextAsync(
        SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<OrderFlowDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new OrderFlowDbContext(options);
        await context.Database.EnsureCreatedAsync();

        return context;
    }

    private static CreateOrderRequest ValidRequest(
        int customerId = 1,
        int productId = 1,
        int quantity = 2)
    {
        return new CreateOrderRequest
        {
            CustomerId = customerId,
            Items =
            [
                new CreateOrderItemRequest
                {
                    ProductId = productId,
                    Quantity = quantity
                }
            ]
        };
    }

    private static async Task SeedCustomerAndProductAsync(
        OrderFlowDbContext context)
    {
        context.Customers.Add(new Customer
        {
            Name = "Test Customer",
            Email = "test@example.com"
        });

        context.Products.Add(new Product
        {
            Name = "Keyboard",
            Price = 4500m,
            StockQuantity = 25
        });

        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task CreateAsync_UsesDatabasePriceToCalculateTotal()
    {
        await using var connection =
            new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using var context = await CreateContextAsync(connection);
        await SeedCustomerAndProductAsync(context);

        var service = new OrderService(context);

        var result = await service.CreateAsync(ValidRequest());

        Assert.NotNull(result);
        Assert.Equal(9000m, result.Total);
        Assert.Equal(4500m, result.Items[0].UnitPrice);
        Assert.Equal(2, result.Items[0].Quantity);
        Assert.Equal(9000m, result.Items[0].LineTotal);
    }

    [Fact]
    public async Task CreateAsync_ReturnsNull_WhenCustomerDoesNotExist()
    {
        await using var connection =
            new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using var context = await CreateContextAsync(connection);
        var service = new OrderService(context);

        var result = await service.CreateAsync(ValidRequest(customerId: 999));

        Assert.Null(result);
        Assert.Empty(await context.Orders.ToListAsync());
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenProductDoesNotExist()
    {
        await using var connection =
            new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using var context = await CreateContextAsync(connection);

        context.Customers.Add(new Customer
        {
            Name = "Test Customer",
            Email = "test@example.com"
        });
        await context.SaveChangesAsync();

        var service = new OrderService(context);

        await Assert.ThrowsAsync<InvalidOrderException>(
            () => service.CreateAsync(ValidRequest(productId: 999)));

        Assert.Empty(await context.Orders.ToListAsync());
    }

    [Fact]
    public async Task GetByIdAsync_PreservesOriginalPriceAfterProductPriceChanges()
    {
        await using var connection =
            new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using var context = await CreateContextAsync(connection);
        await SeedCustomerAndProductAsync(context);

        var service = new OrderService(context);
        var created = await service.CreateAsync(ValidRequest());

        Assert.NotNull(created);

        var product = await context.Products.SingleAsync();
        product.Price = 6000m;
        await context.SaveChangesAsync();

        var retrieved = await service.GetByIdAsync(created.Id);

        Assert.NotNull(retrieved);
        Assert.Equal(9000m, retrieved.Total);
        Assert.Equal(4500m, retrieved.Items[0].UnitPrice);
    }

    [Fact]
    public async Task CreateAsync_RejectsDuplicateProductIds()
    {
        await using var connection =
            new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using var context = await CreateContextAsync(connection);
        await SeedCustomerAndProductAsync(context);

        var request = new CreateOrderRequest
        {
            CustomerId = 1,
            Items =
            [
                new CreateOrderItemRequest
                {
                    ProductId = 1,
                    Quantity = 1
                },
                new CreateOrderItemRequest
                {
                    ProductId = 1,
                    Quantity = 2
                }
            ]
        };

        var service = new OrderService(context);

        await Assert.ThrowsAsync<InvalidOrderException>(
            () => service.CreateAsync(request));

        Assert.Empty(await context.Orders.ToListAsync());
    }
}