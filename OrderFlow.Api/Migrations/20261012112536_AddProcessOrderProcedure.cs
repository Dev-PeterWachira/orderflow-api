using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderFlow.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessOrderProcedure : Migration
    {
        /// <inheritdoc />
                 
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.Sql(
        """
        CREATE OR ALTER PROCEDURE dbo.ProcessOrder
            @OrderId int
        AS
        BEGIN
            SET NOCOUNT ON;
            SET XACT_ABORT ON;

            BEGIN TRY
                BEGIN TRANSACTION;

                -- 1. Atomically claim a Pending order.
                UPDATE dbo.Orders
                SET Status = 1
                WHERE Id = @OrderId
                  AND Status = 0;

                IF @@ROWCOUNT = 0
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1
                        FROM dbo.Orders
                        WHERE Id = @OrderId
                    )
                        THROW 50001, 'Order not found.', 1;

                    THROW 50002,
                        'Order cannot be processed in its current state.',
                        1;
                END;

                -- 2. Validate order items.
                IF NOT EXISTS (
                    SELECT 1
                    FROM dbo.OrderItems
                    WHERE OrderId = @OrderId
                )
                    THROW 50003, 'Order has no items.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM dbo.OrderItems
                    WHERE OrderId = @OrderId
                      AND Quantity <= 0
                )
                    THROW 50004, 'Invalid order item quantity.', 1;

                DECLARE @ExpectedProducts int;
                DECLARE @UpdatedProducts int;
                DECLARE @Total decimal(18, 2);

                SELECT @ExpectedProducts = COUNT(*)
                FROM (
                    SELECT ProductId
                    FROM dbo.OrderItems
                    WHERE OrderId = @OrderId
                    GROUP BY ProductId
                ) AS RequestedProducts;

                -- 3. Reduce stock only where sufficient stock exists.
                -- Aggregate quantities in case duplicate product rows exist.
                UPDATE p
                SET p.StockQuantity =
                    p.StockQuantity - requested.RequiredQuantity
                FROM dbo.Products AS p
                INNER JOIN (
                    SELECT
                        ProductId,
                        SUM(Quantity) AS RequiredQuantity
                    FROM dbo.OrderItems
                    WHERE OrderId = @OrderId
                    GROUP BY ProductId
                ) AS requested
                    ON requested.ProductId = p.Id
                WHERE p.StockQuantity >= requested.RequiredQuantity;

                SET @UpdatedProducts = @@ROWCOUNT;

                IF @UpdatedProducts <> @ExpectedProducts
                    THROW 50005, 'Insufficient product stock.', 1;

                -- 4. Calculate using historical order-item prices.
                SELECT @Total =
                    SUM(UnitPrice * Quantity)
                FROM dbo.OrderItems
                WHERE OrderId = @OrderId;

                -- 5. Record payment.
                INSERT INTO dbo.Payments
                    (OrderId, Amount, CreatedAt)
                VALUES
                    (@OrderId, @Total, SYSUTCDATETIME());

                -- 6. Complete the order.
                UPDATE dbo.Orders
                SET Status = 2
                WHERE Id = @OrderId
                  AND Status = 1;

                IF @@ROWCOUNT <> 1
                    THROW 50006, 'Could not complete the order.', 1;

                COMMIT TRANSACTION;

                SELECT
                    @OrderId AS OrderId,
                    @Total AS Total,
                    2 AS Status;
            END TRY
            BEGIN CATCH
                IF XACT_STATE() <> 0
                    ROLLBACK TRANSACTION;

                THROW;
            END CATCH;
        END;
        """);
}

        

        /// <inheritdoc />
       protected override void Down(MigrationBuilder migrationBuilder)
{
    migrationBuilder.Sql(
        "DROP PROCEDURE IF EXISTS dbo.ProcessOrder;");
}

        }
    }

