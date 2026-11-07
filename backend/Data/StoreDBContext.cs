using ClothingStore.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ClothingStore.Api.Data;

public class StoreDbContext(DbContextOptions<StoreDbContext> options)
    : DbContext(options)
{
    public DbSet<ProductEntity> Products => Set<ProductEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var product = modelBuilder.Entity<ProductEntity>();

        product.Property(p => p.Name).HasMaxLength(120);
        product.Property(p => p.Description).HasMaxLength(2000);
        product.Property(p => p.Category).HasMaxLength(80);
        product.Property(p => p.Price).HasPrecision(18, 2);
        product.Property(p => p.ImageUrl).HasMaxLength(300);
        product.Property(p => p.ImageAlt).HasMaxLength(250);

        product.HasMany(p => p.Details)
            .WithOne()
            .HasForeignKey(d => d.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        var detail = modelBuilder.Entity<ProductDetailEntity>();

        detail.Property(d => d.Name).HasMaxLength(60);
        detail.Property(d => d.Value).HasMaxLength(200);
    }
}