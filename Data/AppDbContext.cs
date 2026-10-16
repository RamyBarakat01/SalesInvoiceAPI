using Microsoft.EntityFrameworkCore;
using SalesInvoiceAPI.Models;

namespace SalesInvoiceAPI.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Customer> Customers { get; set; }

    public DbSet<Product> Products { get; set; }

    public DbSet<Invoice> Invoices { get; set; }

    public DbSet<InvoiceItem> InvoiceItems { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    // Customer -> Invoices (each cumstomer can have multiple invoices while one invoice is only to one customer)
    modelBuilder.Entity<Invoice>()
        .HasOne<Customer>()
        .WithMany()
        .HasForeignKey(i => i.CustomerId)
        .OnDelete(DeleteBehavior.Restrict);

    // Invoice -> InvoiceItems
    modelBuilder.Entity<InvoiceItem>()
        .HasOne<Invoice>()
        .WithMany()
        .HasForeignKey(item => item.InvoiceId);

    // Product -> InvoiceItems
    modelBuilder.Entity<InvoiceItem>()
        .HasOne<Product>()
        .WithMany()
        .HasForeignKey(item => item.ProductId)
        .OnDelete(DeleteBehavior.Restrict);

    // Invoice number must be unique so a unique indxe is created
    modelBuilder.Entity<Invoice>()
        .HasIndex(i => i.InvoiceNumber)
        .IsUnique();

    // Configure decimal precision
    modelBuilder.Entity<Product>()
        .Property(p => p.Price)
        .HasPrecision(18, 2);

    modelBuilder.Entity<Invoice>()
        .Property(i => i.TotalAmount)
        .HasPrecision(18, 2);

    modelBuilder.Entity<InvoiceItem>()
        .Property(item => item.UnitPrice)
        .HasPrecision(18, 2);

    modelBuilder.Entity<InvoiceItem>()
        .Property(item => item.LineTotal)
        .HasPrecision(18, 2);
    
    modelBuilder.Entity<Customer>()
        .Property(c => c.Name)
        .HasMaxLength(100);

    modelBuilder.Entity<Customer>()
        .Property(c => c.Email)
        .HasMaxLength(255);

    modelBuilder.Entity<Customer>()
        .Property(c => c.Phone)
        .HasMaxLength(20);

    modelBuilder.Entity<Product>()
        .Property(p => p.Name)
        .HasMaxLength(100);

    modelBuilder.Entity<Invoice>()
        .Property(i => i.InvoiceNumber)
        .HasMaxLength(30);
}
}

