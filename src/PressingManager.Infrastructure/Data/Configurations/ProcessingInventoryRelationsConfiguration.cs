using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PressingManager.Core.Entities;

namespace PressingManager.Infrastructure.Data.Configurations;

/// <summary>
/// Cles etrangeres du module traitement / stocks vers les modules commandes et authentification.
/// Fichier separe pour ne pas modifier les configurations deja en place. Les entites n'exposent
/// pas de propriete de navigation : la relation est declaree avec HasOne&lt;T&gt;().
/// </summary>
public class OrderStatusHistoryRelationsConfiguration : IEntityTypeConfiguration<OrderStatusHistory>
{
    public void Configure(EntityTypeBuilder<OrderStatusHistory> builder)
    {
        builder.HasOne<Order>()
            .WithMany()
            .HasForeignKey(h => h.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(h => h.ChangedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class StockMovementUserRelationConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
