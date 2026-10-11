using Microsoft.EntityFrameworkCore;
using OrderFlow.Api.Data;
using OrderFlow.Api.DTOs.Reports;
using OrderFlow.Api.Models;
using OrderFlow.Api.Models.Reporting;

namespace OrderFlow.Api.Services;

public class ReportService(OrderFlowDbContext dbContext) : IReportService
{
    public async Task<SalesSummaryResponse> GetSalesAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        var completedStatus = (int)OrderStatus.Completed;

        var result = await dbContext.SalesSummaryRows
            .FromSqlInterpolated($"""
                SELECT
                    COUNT(DISTINCT o.Id) AS OrderCount,
                    COALESCE(SUM(oi.Quantity), 0) AS ItemsSold,
                    COALESCE(SUM(oi.UnitPrice * oi.Quantity), 0) AS Revenue
                FROM dbo.Orders AS o
                LEFT JOIN dbo.OrderItems AS oi ON oi.OrderId = o.Id
                WHERE o.Status = {completedStatus}
                  AND o.CreatedAt >= {startDate}
                  AND o.CreatedAt < {endDate}
                """)
            .AsNoTracking()
            .SingleAsync(cancellationToken);

        return new SalesSummaryResponse
        {
            OrderCount = result.OrderCount,
            ItemsSold = result.ItemsSold,
            Revenue = result.Revenue
        };
    }

    public async Task<IReadOnlyList<DailySalesResponse>> GetDailySalesAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        var completedStatus = (int)OrderStatus.Completed;

        var rows = await dbContext.DailySalesRows
            .FromSqlInterpolated($"""
                SELECT
                    CAST(o.CreatedAt AS date) AS [Date],
                    COUNT(DISTINCT o.Id) AS OrderCount,
                    SUM(oi.Quantity) AS ItemsSold,
                    SUM(oi.UnitPrice * oi.Quantity) AS Revenue
                FROM dbo.Orders AS o
                INNER JOIN dbo.OrderItems AS oi ON oi.OrderId = o.Id
                WHERE o.Status = {completedStatus}
                  AND o.CreatedAt >= {startDate}
                  AND o.CreatedAt < {endDate}
                GROUP BY CAST(o.CreatedAt AS date)
                ORDER BY CAST(o.CreatedAt AS date)
                """)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return rows.Select(row => new DailySalesResponse
        {
            Date = row.Date,
            OrderCount = row.OrderCount,
            ItemsSold = row.ItemsSold,
            Revenue = row.Revenue
        }).ToList();
    }

    public async Task<IReadOnlyList<ProductSalesResponse>> GetProductSalesAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        var completedStatus = (int)OrderStatus.Completed;

        var rows = await dbContext.ProductSalesRows
            .FromSqlInterpolated($"""
                SELECT
                    p.Id AS ProductId,
                    p.Name AS ProductName,
                    SUM(oi.Quantity) AS ItemsSold,
                    SUM(oi.UnitPrice * oi.Quantity) AS Revenue
                FROM dbo.Orders AS o
                INNER JOIN dbo.OrderItems AS oi ON oi.OrderId = o.Id
                INNER JOIN dbo.Products AS p ON p.Id = oi.ProductId
                WHERE o.Status = {completedStatus}
                  AND o.CreatedAt >= {startDate}
                  AND o.CreatedAt < {endDate}
                GROUP BY p.Id, p.Name
                ORDER BY SUM(oi.UnitPrice * oi.Quantity) DESC
                """)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return rows.Select(MapProductSales).ToList();
    }

    public async Task<IReadOnlyList<ProductSalesResponse>> GetTopProductsAsync(
        DateTime startDate,
        DateTime endDate,
        int count = 10,
        CancellationToken cancellationToken = default)
    {
        if (count is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(count), "Count must be between 1 and 100.");
        }

        var completedStatus = (int)OrderStatus.Completed;

        var rows = await dbContext.ProductSalesRows
            .FromSqlInterpolated($"""
                SELECT TOP ({count})
                    p.Id AS ProductId,
                    p.Name AS ProductName,
                    SUM(oi.Quantity) AS ItemsSold,
                    SUM(oi.UnitPrice * oi.Quantity) AS Revenue
                FROM dbo.Orders AS o
                INNER JOIN dbo.OrderItems AS oi ON oi.OrderId = o.Id
                INNER JOIN dbo.Products AS p ON p.Id = oi.ProductId
                WHERE o.Status = {completedStatus}
                  AND o.CreatedAt >= {startDate}
                  AND o.CreatedAt < {endDate}
                GROUP BY p.Id, p.Name
                ORDER BY SUM(oi.UnitPrice * oi.Quantity) DESC
                """)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return rows.Select(MapProductSales).ToList();
    }

    public async Task<IReadOnlyList<CustomerOrderHistoryResponse>>
        GetCustomerOrderHistoryAsync(
            int customerId,
            CancellationToken cancellationToken = default)
    {
        var rows = await dbContext.CustomerOrderHistoryRows
            .FromSqlInterpolated($"""
                SELECT
                    o.Id AS OrderId,
                    o.CreatedAt,
                    CAST(o.Status AS int) AS Status,
                    COALESCE(SUM(oi.Quantity), 0) AS ItemsCount,
                    COALESCE(SUM(oi.UnitPrice * oi.Quantity), 0) AS Total
                FROM dbo.Orders AS o
                LEFT JOIN dbo.OrderItems AS oi ON oi.OrderId = o.Id
                WHERE o.CustomerId = {customerId}
                GROUP BY o.Id, o.CreatedAt, o.Status
                ORDER BY o.CreatedAt DESC, o.Id DESC
                """)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return rows.Select(row => new CustomerOrderHistoryResponse
        {
            OrderId = row.OrderId,
            CreatedAt = row.CreatedAt,
            Status = (OrderStatus)row.Status,
            ItemsCount = row.ItemsCount,
            Total = row.Total
        }).ToList();
    }

    private static ProductSalesResponse MapProductSales(ProductSalesRow row)
    {
        return new ProductSalesResponse
        {
            ProductId = row.ProductId,
            ProductName = row.ProductName,
            ItemsSold = row.ItemsSold,
            Revenue = row.Revenue
        };
    }
}