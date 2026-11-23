using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PressingManager.Core.Entities;

namespace PressingManager.Infrastructure.Data.Configurations;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.CustomerNumber).IsRequired().HasMaxLength(20);
        builder.Property(c => c.FullName).IsRequired().HasMaxLength(150);
        builder.Property(c => c.Phone).IsRequired().HasMaxLength(30);
        builder.Property(c => c.Address).HasMaxLength(300);
        builder.Property(c => c.Notes).HasMaxLength(1000);

        builder.HasIndex(c => c.CustomerNumber).IsUnique();
        builder.HasIndex(c => c.Phone);
        builder.HasIndex(c => c.FullName);
    }
}

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.OrderNumber).IsRequired().HasMaxLength(30);
        builder.Property(o => o.Notes).HasMaxLength(1000);
        builder.Property(o => o.Discount).HasPrecision(18, 2);
        builder.Property(o => o.Fees).HasPrecision(18, 2);

        // Sous-total et total sont calcules, pas stockes
        builder.Ignore(o => o.Subtotal);
        builder.Ignore(o => o.Total);

        // Un client avec des commandes ne peut pas etre supprime (on le desactive)
        builder.HasOne(o => o.Customer)
            .WithMany(c => c.Orders)
            .HasForeignKey(o => o.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(o => o.OrderNumber).IsUnique();
        builder.HasIndex(o => o.CustomerId);
        builder.HasIndex(o => o.CreatedAt);
    }
}

public sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("OrderItems");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.ItemType).IsRequired().HasMaxLength(100);
        builder.Property(i => i.Observations).HasMaxLength(500);
        builder.Property(i => i.UnitPrice).HasPrecision(18, 2);

        builder.Ignore(i => i.LineTotal);

        // Si on supprime une commande, ses lignes partent avec
        builder.HasOne(i => i.Order)
            .WithMany(o => o.Items)
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(i => i.OrderId);
    }
}