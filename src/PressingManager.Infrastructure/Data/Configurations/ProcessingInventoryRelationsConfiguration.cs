using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PressingManager.Core.Entities;

namespace PressingManager.Infrastructure.Data.Configurations;

/// <summary>
/// Cles etrangeres du module traitement / stocks vers les modules commandes et authentification
/// (et de OrderItem vers Services : une ligne de commande reference toujours un service du catalogue).
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

public class OrderItemServiceRelationConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        // Restrict : un service deja utilise dans une commande ne peut pas etre supprime
        // (on le desactive avec ServiceCatalogService.SetActiveAsync).
        builder.HasOne<Service>()
            .WithMany()
            .HasForeignKey(i => i.ServiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => i.ServiceId);
    }
}
