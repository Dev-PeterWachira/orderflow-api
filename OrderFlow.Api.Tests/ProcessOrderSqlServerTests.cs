
using System;
using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Api.Data;
using OrderFlow.Api.Models;

namespace OrderFlow.Api.Tests;

public class ProcessOrderSqlServerTests
{
    private readonly string _connectionString;

    public ProcessOrderSqlServerTests()
    {
        _connectionString =
            Environment.GetEnvironmentVariable(
                "ORDERFLOW_SQLSERVER_TEST_CONNECTION")
            ?? throw new InvalidOperationException(
                "Set ORDERFLOW_SQLSERVER_TEST_CONNECTION before running SQL Server integration tests.");
    }

          
[Fact]
public async Task Order_UpdateWithStaleRowVersion_ThrowsConcurrencyException()
{
    var orderId = await CreatePendingOrderAsync();

    try
    {
        var options = new DbContextOptionsBuilder<OrderFlowDbContext>()
            .UseSqlServer(_connectionString)
            .Options;

        await using var contextA = new OrderFlowDbContext(options);
        await using var contextB = new OrderFlowDbContext(options);

        // Both contexts read the same original rowversion.
        var orderA = await contextA.Orders
            .SingleAsync(o => o.Id == orderId);

        var orderB = await contextB.Orders
            .SingleAsync(o => o.Id == orderId);

        // Context A updates the order first.
        orderA.Status = OrderStatus.Cancelled;
        await contextA.SaveChangesAsync();

        // Context B attempts to save using its stale rowversion.
        orderB.Status = OrderStatus.Processing;

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => contextB.SaveChangesAsync());

        // Verify that context B did not overwrite context A's update.
        var persistedStatus = await contextA.Orders
            .AsNoTracking()
            .Where(o => o.Id == orderId)
            .Select(o => o.Status)
            .SingleAsync();

        Assert.Equal(OrderStatus.Cancelled, persistedStatus);
    }
    finally
    {
        await CleanupOrderAsync(orderId);
    }
}


    // Creates one customer, one product, one order, and one order item.
    private async Task<int> CreatePendingOrderAsync(
        int stockQuantity = 10,
        int orderQuantity = 2,
        decimal unitPrice = 25m)
    {
        await using var connection =
            new SqlConnection(_connectionString);

        await connection.OpenAsync();

        await using var command = connection.CreateCommand();

        command.CommandText =
            """
            SET NOCOUNT ON;

            INSERT INTO dbo.Customers (Name, Email)
            VALUES (@Name, @Email);

            DECLARE @CustomerId int =
                CONVERT(int, SCOPE_IDENTITY());

            INSERT INTO dbo.Products (Name, Price, StockQuantity)
            VALUES (@ProductName, @Price, @Stock);

            DECLARE @ProductId int =
                CONVERT(int, SCOPE_IDENTITY());

            INSERT INTO dbo.Orders (CustomerId, Status, CreatedAt)
            VALUES (@CustomerId, 0, SYSUTCDATETIME());

            DECLARE @OrderId int =
                CONVERT(int, SCOPE_IDENTITY());

            INSERT INTO dbo.OrderItems
                (OrderId, ProductId, Quantity, UnitPrice)
            VALUES
                (@OrderId, @ProductId, @Quantity, @UnitPrice);

            SELECT @OrderId;
            """;

        command.Parameters.AddWithValue(
            "@Name", $"Test-{Guid.NewGuid():N}");

        command.Parameters.AddWithValue(
            "@Email", $"{Guid.NewGuid():N}@example.test");

        command.Parameters.AddWithValue(
            "@ProductName", $"Product-{Guid.NewGuid():N}");

        command.Parameters.AddWithValue("@Price", unitPrice);
        command.Parameters.AddWithValue("@Stock", stockQuantity);
        command.Parameters.AddWithValue("@Quantity", orderQuantity);
        command.Parameters.AddWithValue("@UnitPrice", unitPrice);

        return Convert.ToInt32(
            await command.ExecuteScalarAsync());
    }

    // Creates two different orders that share a product with one item left.
    private async Task<(
        int OrderAId,
        int OrderBId,
        int ProductId,
        int CustomerAId,
        int CustomerBId)> CreateTwoOrdersSharingProductAsync()
    {
        await using var connection =
            new SqlConnection(_connectionString);

        await connection.OpenAsync();

        await using var command = connection.CreateCommand();

        command.CommandText =
            """
            SET NOCOUNT ON;

            INSERT INTO dbo.Customers (Name, Email)
            VALUES (@NameA, @EmailA);

            DECLARE @CustomerAId int =
                CONVERT(int, SCOPE_IDENTITY());

            INSERT INTO dbo.Customers (Name, Email)
            VALUES (@NameB, @EmailB);

            DECLARE @CustomerBId int =
                CONVERT(int, SCOPE_IDENTITY());

            INSERT INTO dbo.Products (Name, Price, StockQuantity)
            VALUES (@ProductName, 25.00, 1);

            DECLARE @ProductId int =
                CONVERT(int, SCOPE_IDENTITY());

            INSERT INTO dbo.Orders (CustomerId, Status, CreatedAt)
            VALUES (@CustomerAId, 0, SYSUTCDATETIME());

            DECLARE @OrderAId int =
                CONVERT(int, SCOPE_IDENTITY());

            INSERT INTO dbo.Orders (CustomerId, Status, CreatedAt)
            VALUES (@CustomerBId, 0, SYSUTCDATETIME());

            DECLARE @OrderBId int =
                CONVERT(int, SCOPE_IDENTITY());

            INSERT INTO dbo.OrderItems
                (OrderId, ProductId, Quantity, UnitPrice)
            VALUES
                (@OrderAId, @ProductId, 1, 25.00),
                (@OrderBId, @ProductId, 1, 25.00);

            SELECT
                @OrderAId,
                @OrderBId,
                @ProductId,
                @CustomerAId,
                @CustomerBId;
            """;

        var uniqueA = Guid.NewGuid().ToString("N");
        var uniqueB = Guid.NewGuid().ToString("N");

        command.Parameters.AddWithValue(
            "@NameA", $"Customer-{uniqueA}");

        command.Parameters.AddWithValue(
            "@EmailA", $"{uniqueA}@example.test");

        command.Parameters.AddWithValue(
            "@NameB", $"Customer-{uniqueB}");

        command.Parameters.AddWithValue(
            "@EmailB", $"{uniqueB}@example.test");

        command.Parameters.AddWithValue(
            "@ProductName", $"Shared-{Guid.NewGuid():N}");

        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            throw new InvalidOperationException(
                "Could not create test orders.");
        }

        return (
            reader.GetInt32(0),
            reader.GetInt32(1),
            reader.GetInt32(2),
            reader.GetInt32(3),
            reader.GetInt32(4));
    }

    // Executes dbo.ProcessOrder directly against SQL Server.
    private async Task ExecuteProcedureAsync(int orderId)
    {
        await using var connection =
            new SqlConnection(_connectionString);

        await connection.OpenAsync();

        await using var command = connection.CreateCommand();

        command.CommandText = "dbo.ProcessOrder";
        command.CommandType = CommandType.StoredProcedure;

        command.Parameters.AddWithValue("@OrderId", orderId);

        await command.ExecuteNonQueryAsync();
    }

    // Reads the persisted order, product stock, and payment state.
    private async Task<(
        int Status,
        int Stock,
        int Payments,
        decimal? Amount)> ReadOrderStateAsync(int orderId)
    {
        await using var connection =
            new SqlConnection(_connectionString);

        await connection.OpenAsync();

        await using var command = connection.CreateCommand();

        command.CommandText =
            """
            SELECT
                o.Status,
                p.StockQuantity,
                (
                    SELECT COUNT(*)
                    FROM dbo.Payments AS pay
                    WHERE pay.OrderId = o.Id
                ) AS PaymentCount,
                (
                    SELECT MAX(pay.Amount)
                    FROM dbo.Payments AS pay
                    WHERE pay.OrderId = o.Id
                ) AS PaymentAmount
            FROM dbo.Orders AS o
            INNER JOIN dbo.OrderItems AS oi
                ON oi.OrderId = o.Id
            INNER JOIN dbo.Products AS p
                ON p.Id = oi.ProductId
            WHERE o.Id = @OrderId;
            """;

        command.Parameters.AddWithValue("@OrderId", orderId);

        await using var reader =
            await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            throw new InvalidOperationException(
                "Test order was not found.");
        }

        return (
            reader.GetInt32(0),
            reader.GetInt32(1),
            reader.GetInt32(2),
            reader.IsDBNull(3) ? null : reader.GetDecimal(3));
    }

    // Cleans up the records belonging to a single-order test.
    private async Task CleanupOrderAsync(int orderId)
    {
        await using var connection =
            new SqlConnection(_connectionString);

        await connection.OpenAsync();

        await using var command = connection.CreateCommand();

        command.CommandText =
            """
            SET XACT_ABORT ON;

            BEGIN TRANSACTION;

            DECLARE @CustomerId int;
            DECLARE @ProductId int;

            SELECT
                @CustomerId = o.CustomerId,
                @ProductId = oi.ProductId
            FROM dbo.Orders AS o
            INNER JOIN dbo.OrderItems AS oi
                ON oi.OrderId = o.Id
            WHERE o.Id = @OrderId;

            DELETE FROM dbo.Payments
            WHERE OrderId = @OrderId;

            DELETE FROM dbo.Orders
            WHERE Id = @OrderId;

            DELETE FROM dbo.Products
            WHERE Id = @ProductId;

            DELETE FROM dbo.Customers
            WHERE Id = @CustomerId;

            COMMIT TRANSACTION;
            """;

        command.Parameters.AddWithValue("@OrderId", orderId);

        await command.ExecuteNonQueryAsync();
    }

    // Cleans up two orders that share one product.
    private async Task CleanupSharedProductOrdersAsync(
        (int OrderAId, int OrderBId, int ProductId,
         int CustomerAId, int CustomerBId) data)
    {
        await using var connection =
            new SqlConnection(_connectionString);

        await connection.OpenAsync();

        await using var command = connection.CreateCommand();

        command.CommandText =
            """
            SET XACT_ABORT ON;

            BEGIN TRANSACTION;

            DELETE FROM dbo.Payments
            WHERE OrderId IN (@OrderAId, @OrderBId);

            DELETE FROM dbo.Orders
            WHERE Id IN (@OrderAId, @OrderBId);

            DELETE FROM dbo.Products
            WHERE Id = @ProductId;

            DELETE FROM dbo.Customers
            WHERE Id IN (@CustomerAId, @CustomerBId);

            COMMIT TRANSACTION;
            """;

        command.Parameters.AddWithValue("@OrderAId", data.OrderAId);
        command.Parameters.AddWithValue("@OrderBId", data.OrderBId);
        command.Parameters.AddWithValue("@ProductId", data.ProductId);
        command.Parameters.AddWithValue("@CustomerAId", data.CustomerAId);
        command.Parameters.AddWithValue("@CustomerBId", data.CustomerBId);

        await command.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task ProcessOrder_Success_ReducesStockAndRecordsPayment()
    {
        var orderId = await CreatePendingOrderAsync();

        try
        {
            await ExecuteProcedureAsync(orderId);

            var state = await ReadOrderStateAsync(orderId);

            Assert.Equal(2, state.Status);
            Assert.Equal(8, state.Stock);
            Assert.Equal(1, state.Payments);
            Assert.Equal(50m, state.Amount);
        }
        finally
        {
            await CleanupOrderAsync(orderId);
        }
    }

    [Fact]
    public async Task ProcessOrder_InsufficientStock_RollsBackChanges()
    {
        var orderId = await CreatePendingOrderAsync(
            stockQuantity: 1,
            orderQuantity: 2);

        try
        {
            await Assert.ThrowsAsync<SqlException>(
                () => ExecuteProcedureAsync(orderId));

            var state = await ReadOrderStateAsync(orderId);

            Assert.Equal(0, state.Status);
            Assert.Equal(1, state.Stock);
            Assert.Equal(0, state.Payments);
            Assert.Null(state.Amount);
        }
        finally
        {
            await CleanupOrderAsync(orderId);
        }
    }

    [Fact]
    public async Task ProcessOrder_DuplicateAttempt_DoesNotChangeStateAgain()
    {
        var orderId = await CreatePendingOrderAsync();

        try
        {
            await ExecuteProcedureAsync(orderId);

            await Assert.ThrowsAsync<SqlException>(
                () => ExecuteProcedureAsync(orderId));

            var state = await ReadOrderStateAsync(orderId);

            Assert.Equal(2, state.Status);
            Assert.Equal(8, state.Stock);
            Assert.Equal(1, state.Payments);
            Assert.Equal(50m, state.Amount);
        }
        finally
        {
            await CleanupOrderAsync(orderId);
        }
    }

    [Fact]
    public async Task ProcessOrder_MissingOrder_ThrowsSqlException()
    {
        await Assert.ThrowsAsync<SqlException>(
            () => ExecuteProcedureAsync(-999999));
    }

    [Fact]
    public async Task ProcessOrder_ConcurrentAttempts_OnlyOneSucceeds()
    {
        var orderId = await CreatePendingOrderAsync();

        try
        {
            var results = await Task.WhenAll(
                AttemptAsync(orderId),
                AttemptAsync(orderId));

            Assert.Equal(1, results.Count(success => success));
            Assert.Equal(1, results.Count(success => !success));

            var state = await ReadOrderStateAsync(orderId);

            Assert.Equal(2, state.Status);
            Assert.Equal(8, state.Stock);
            Assert.Equal(1, state.Payments);
            Assert.Equal(50m, state.Amount);
        }
        finally
        {
            await CleanupOrderAsync(orderId);
        }
    }

    [Fact]
    public async Task ProcessOrder_TwoOrdersCompeteForLastItem_OnlyOneSucceeds()
    {
        var data = await CreateTwoOrdersSharingProductAsync();

        try
        {
            var results = await Task.WhenAll(
                AttemptStockRaceAsync(data.OrderAId),
                AttemptStockRaceAsync(data.OrderBId));

            Assert.Equal(1, results.Count(result => result.Succeeded));
            Assert.Equal(1, results.Count(result => !result.Succeeded));

            var failedAttempt = results.Single(
                result => !result.Succeeded);

            // SQL Server error 50005 represents insufficient stock
            // in the current dbo.ProcessOrder procedure.
            Assert.Equal(50005, failedAttempt.ErrorNumber);

            var orderA = await ReadOrderStateAsync(data.OrderAId);
            var orderB = await ReadOrderStateAsync(data.OrderBId);

            // Both orders reference the same product.
            Assert.Equal(0, orderA.Stock);
            Assert.Equal(0, orderB.Stock);

            // One completed (2), one pending (0).
            Assert.Equal(2, orderA.Status + orderB.Status);

            Assert.Equal(1, orderA.Payments + orderB.Payments);

            Assert.Equal(
                25m,
                (orderA.Amount ?? 0m) + (orderB.Amount ?? 0m));
        }
        finally
        {
            await CleanupSharedProductOrdersAsync(data);
        }
    }

    private async Task<bool> AttemptAsync(int orderId)
    {
        try
        {
            await ExecuteProcedureAsync(orderId);
            return true;
        }
        catch (SqlException)
        {
            return false;
        }
    }

    private async Task<(bool Succeeded, int? ErrorNumber)>
        AttemptStockRaceAsync(int orderId)
    {
        try
        {
            await ExecuteProcedureAsync(orderId);
            return (true, null);
        }
        catch (SqlException ex)
        {
            return (false, ex.Number);
        }
    }
}