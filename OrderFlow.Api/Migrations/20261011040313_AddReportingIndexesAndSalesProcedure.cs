using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderFlow.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddReportingIndexesAndSalesProcedure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_CustomerId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_OrderItems_ProductId",
                table: "OrderItems");

            migrationBuilder.CreateTable(
                name: "CustomerOrderHistoryRows",
                columns: table => new
                {
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ItemsCount = table.Column<int>(type: "int", nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "DailySalesRows",
                columns: table => new
                {
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OrderCount = table.Column<int>(type: "int", nullable: false),
                    ItemsSold = table.Column<int>(type: "int", nullable: false),
                    Revenue = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "ProductSalesRows",
                columns: table => new
                {
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    ProductName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ItemsSold = table.Column<int>(type: "int", nullable: false),
                    Revenue = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "SalesSummaryRows",
                columns: table => new
                {
                    OrderCount = table.Column<int>(type: "int", nullable: false),
                    ItemsSold = table.Column<int>(type: "int", nullable: false),
                    Revenue = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CustomerId_CreatedAt",
                table: "Orders",
                columns: new[] { "CustomerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Status_CreatedAt",
                table: "Orders",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_ProductId_OrderId",
                table: "OrderItems",
                columns: new[] { "ProductId", "OrderId" });

            migrationBuilder.Sql(
    """
    EXEC(N'
    CREATE OR ALTER PROCEDURE dbo.GetSalesSummary
        @StartDate datetime2,
        @EndDate datetime2,
        @CompletedStatus int
    AS
    BEGIN
        SET NOCOUNT ON;

        SELECT
            COUNT(DISTINCT o.Id) AS OrderCount,
            COALESCE(SUM(oi.Quantity), 0) AS ItemsSold,
            COALESCE(SUM(oi.UnitPrice * oi.Quantity), 0) AS Revenue
        FROM dbo.Orders AS o
        LEFT JOIN dbo.OrderItems AS oi ON oi.OrderId = o.Id
        WHERE o.Status = @CompletedStatus
          AND o.CreatedAt >= @StartDate
          AND o.CreatedAt < @EndDate;
    END
    ');
    """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
    "DROP PROCEDURE IF EXISTS dbo.GetSalesSummary;");
            migrationBuilder.DropTable(
                name: "CustomerOrderHistoryRows");

            migrationBuilder.DropTable(
                name: "DailySalesRows");

            migrationBuilder.DropTable(
                name: "ProductSalesRows");

            migrationBuilder.DropTable(
                name: "SalesSummaryRows");

            migrationBuilder.DropIndex(
                name: "IX_Orders_CustomerId_CreatedAt",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_Status_CreatedAt",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_OrderItems_ProductId_OrderId",
                table: "OrderItems");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CustomerId",
                table: "Orders",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_ProductId",
                table: "OrderItems",
                column: "ProductId");
        }
    }
}
