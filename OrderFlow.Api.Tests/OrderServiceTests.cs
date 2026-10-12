
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
public async Task ProcessAsync_CompletesOrderAndReducesStock()
{
    await using var connection =
        new SqliteConnection("Data Source=:memory:");
    await connection.OpenAsync();

    await using var context = await CreateContextAsync(connection);
    await SeedCustomerAndProductAsync(context);

    var service = new OrderService(context);
    var created = await service.CreateAsync(ValidRequest());

    Assert.NotNull(created);

    var result = await service.ProcessAsync(created.Id);

    Assert.NotNull(result);
    Assert.Equal(OrderStatus.Completed, result.Status);

    var product = await context.Products
        .AsNoTracking()
        .SingleAsync();

    Assert.Equal(23, product.StockQuantity);

    var payment = await context.Payments
        .AsNoTracking()
        .SingleAsync();

    Assert.Equal(created.Id, payment.OrderId);
    Assert.Equal(9000m, payment.Amount);
}


[Fact]
public async Task ProcessAsync_InsufficientStock_RollsBackAllChanges()
{
    await using var connection =
        new SqliteConnection("Data Source=:memory:");
    await connection.OpenAsync();

    await using var context = await CreateContextAsync(connection);
    await SeedCustomerAndProductAsync(context);

    context.Products.Add(new Product
    {
        Name = "Monitor",
        Price = 15000m,
        StockQuantity = 1
    });

    await context.SaveChangesAsync();

    var service = new OrderService(context);

    var request = new CreateOrderRequest
    {
        CustomerId = 1,
        Items =
        [
            new CreateOrderItemRequest
            {
                ProductId = 1,
                Quantity = 2
            },
            new CreateOrderItemRequest
            {
                ProductId = 2,
                Quantity = 2
            }
        ]
    };

    var created = await service.CreateAsync(request);
    Assert.NotNull(created);

    await Assert.ThrowsAsync<InvalidOrderException>(
        () => service.ProcessAsync(created.Id));

    var products = await context.Products
        .AsNoTracking()
        .OrderBy(p => p.Id)
        .ToListAsync();

    Assert.Equal(25, products[0].StockQuantity);
    Assert.Equal(1, products[1].StockQuantity);

    var order = await context.Orders
        .AsNoTracking()
        .SingleAsync(o => o.Id == created.Id);

    Assert.Equal(OrderStatus.Pending, order.Status);
    Assert.Empty(await context.Payments.AsNoTracking().ToListAsync());
}

 
[Fact]
public async Task ProcessAsync_RejectsDuplicateProcessing()
{
    await using var connection =
        new SqliteConnection("Data Source=:memory:");
    await connection.OpenAsync();

    await using var context = await CreateContextAsync(connection);
    await SeedCustomerAndProductAsync(context);

    var service = new OrderService(context);
    var created = await service.CreateAsync(ValidRequest());

    Assert.NotNull(created);

    await service.ProcessAsync(created.Id);

    await Assert.ThrowsAsync<InvalidOrderException>(
        () => service.ProcessAsync(created.Id));

    var product = await context.Products
        .AsNoTracking()
        .SingleAsync();

    Assert.Equal(23, product.StockQuantity);
    Assert.Single(await context.Payments.AsNoTracking().ToListAsync());
}

 
[Fact]
public async Task ProcessAsync_PaymentInsertFails_RollsBackStockAndStatus()
{
    await using var connection =
        new SqliteConnection("Data Source=:memory:");
    await connection.OpenAsync();

    await using var context = await CreateContextAsync(connection);
    await SeedCustomerAndProductAsync(context);

    var service = new OrderService(context);
    var created = await service.CreateAsync(ValidRequest());

    Assert.NotNull(created);

    context.Payments.Add(new Payment
    {
        OrderId = created.Id,
        Amount = 1m
    });

    await context.SaveChangesAsync();

    await Assert.ThrowsAsync<DbUpdateException>(
        () => service.ProcessAsync(created.Id));

    var product = await context.Products
        .AsNoTracking()
        .SingleAsync();

    var order = await context.Orders
        .AsNoTracking()
        .SingleAsync(o => o.Id == created.Id);

    Assert.Equal(25, product.StockQuantity);
    Assert.Equal(OrderStatus.Pending, order.Status);
    Assert.Single(await context.Payments.AsNoTracking().ToListAsync());
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