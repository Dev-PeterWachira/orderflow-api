
using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Xunit;

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

            DECLARE @CustomerId int = SCOPE_IDENTITY();

            INSERT INTO dbo.Products (Name, Price, StockQuantity)
            VALUES (@ProductName, @Price, @Stock);

            DECLARE @ProductId int = SCOPE_IDENTITY();

            INSERT INTO dbo.Orders (CustomerId, Status, CreatedAt)
            VALUES (@CustomerId, 0, SYSUTCDATETIME());

            DECLARE @OrderId int = SCOPE_IDENTITY();

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
}