
using Microsoft.EntityFrameworkCore;
using OrderFlow.Api.Models;
using OrderFlow.Api.Models.Reporting;

namespace OrderFlow.Api.Data
{
    public class OrderFlowDbContext : DbContext
    {
        public OrderFlowDbContext(DbContextOptions<OrderFlowDbContext> options) : base(options)
        {
            
        }

        public DbSet<Customer> Customers => Set<Customer>();
        public DbSet<Product> Products => Set<Product>();
        public DbSet<Order> Orders => Set<Order>();
        public DbSet<OrderItem> OrderItems => Set<OrderItem>();

        public DbSet<SalesSummaryRow> SalesSummaryRows => Set<SalesSummaryRow>();
        public DbSet<DailySalesRow> DailySalesRows => Set<DailySalesRow>();
        public DbSet<ProductSalesRow> ProductSalesRows => Set<ProductSalesRow>();
        public DbSet<CustomerOrderHistoryRow> CustomerOrderHistoryRows => Set<CustomerOrderHistoryRow>();

        public DbSet<Payment> Payments => Set<Payment>();


        protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    if(Database.IsSqlServer())
            {
                modelBuilder.Entity<Order>()
                     .Property(o => o.RowVersion)
                     .IsRowVersion();
            }
            else
            {
                // keep SQLite-based service tests working without SQL Server rowVersion
                modelBuilder.Entity<Order>()
                      .Ignore(o => o.RowVersion);
            }

    modelBuilder.Entity<Customer>()
        .HasMany(c => c.Orders)
        .WithOne(o => o.Customer)
        .HasForeignKey(o => o.CustomerId)
        .OnDelete(DeleteBehavior.Restrict);

    modelBuilder.Entity<Order>()
        .HasMany(o => o.OrderItems)
        .WithOne(oi => oi.Order)
        .HasForeignKey(oi => oi.OrderId)
        .OnDelete(DeleteBehavior.Cascade);

    modelBuilder.Entity<OrderItem>()
        .Property(oi => oi.UnitPrice)
        .HasPrecision(18,2);

    modelBuilder.Entity<Product>()
        .HasMany(p => p.OrderItems)
        .WithOne(oi => oi.Product)
        .HasForeignKey(oi => oi.ProductId)
        .OnDelete(DeleteBehavior.Restrict);

    modelBuilder.Entity<Product>()
        .Property(p => p.Price)
        .HasPrecision(18, 2);

    modelBuilder.Entity<Product>()
        .Property(p => p.Name)
        .IsRequired()
        .HasMaxLength(200);

    modelBuilder.Entity<Customer>()
        .Property(c => c.Name)
        .IsRequired()
        .HasMaxLength(200);

    modelBuilder.Entity<Customer>()
        .Property(c => c.Email)
        .IsRequired()
        .HasMaxLength(320);

    modelBuilder.Entity<Customer>()
        .HasIndex(c => c.Email)
        .IsUnique()
        .HasDatabaseName("IX_Customers_Email");    
        

    modelBuilder.Entity<Order>()
    .HasIndex(o => new { o.Status, o.CreatedAt })
    .HasDatabaseName("IX_Orders_Status_CreatedAt");

    modelBuilder.Entity<Order>()
    .HasIndex(o => new { o.CustomerId, o.CreatedAt })
    .HasDatabaseName("IX_Orders_CustomerId_CreatedAt");

    modelBuilder.Entity<OrderItem>()
    .HasIndex(oi => new { oi.ProductId, oi.OrderId })
    .HasDatabaseName("IX_OrderItems_ProductId_OrderId");


    modelBuilder.Entity<SalesSummaryRow>()
    .HasNoKey()
    .Property(x => x.Revenue)
    .HasPrecision(18,2);
    modelBuilder.Entity<DailySalesRow>()
    .HasNoKey()
    .Property(x => x.Revenue)
    .HasPrecision(18,2);
    modelBuilder.Entity<ProductSalesRow>()
    .HasNoKey()
    .Property(x => x.Revenue)
    .HasPrecision(18,2);
    modelBuilder.Entity<CustomerOrderHistoryRow>()
    .HasNoKey()
    .Property(x => x.Total)
    .HasPrecision(18,2);


    modelBuilder.Entity<Payment>()
    .HasOne(p => p.Order)
    .WithMany()
    .HasForeignKey(p => p.OrderId)
    .OnDelete(DeleteBehavior.Restrict);

    modelBuilder.Entity<Payment>()
    .Property(p => p.Amount)
    .HasPrecision(18,2);

    modelBuilder.Entity<Payment>()
    .HasIndex(p => p.OrderId)
    .IsUnique()
    .HasDatabaseName("IX_Payments_OrderId");
}
    }
}