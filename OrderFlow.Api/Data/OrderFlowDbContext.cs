
using Microsoft.EntityFrameworkCore;
using OrderFlow.Api.Models;

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


        protected override void OnModelCreating(ModelBuilder modelBuilder)
{
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
}
    }
}