using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PressingManager.Core.Entities;

namespace PressingManager.Infrastructure.Data.Configurations;

/// <summary>
/// OrderId et ChangedByUserId n'ont pas encore de relation EF : les entites Order et User
/// appartiennent a d'autres modules. La relation sera ajoutee a l'integration.
/// </summary>
public class OrderStatusHistoryConfiguration : IEntityTypeConfiguration<OrderStatusHistory>
{
    public void Configure(EntityTypeBuilder<OrderStatusHistory> builder)
    {
        builder.ToTable("OrderStatusHistory");

        builder.HasKey(h => h.Id);

        builder.Property(h => h.Comment).HasMaxLength(500);

        builder.HasIndex(h => new { h.OrderId, h.ChangedAt });
    }
}
