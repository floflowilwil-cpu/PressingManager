using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PressingManager.Core.Entities;

namespace PressingManager.Infrastructure.Data.Configurations;

public class ServicePriceConfiguration : IEntityTypeConfiguration<ServicePrice>
{
    public void Configure(EntityTypeBuilder<ServicePrice> builder)
    {
        builder.ToTable("ServicePrices");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Amount).HasPrecision(18, 2);

        // Un service n'est jamais supprime physiquement : on le desactive.
        builder.HasOne(p => p.Service)
            .WithMany(s => s.Prices)
            .HasForeignKey(p => p.ServiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => new { p.ServiceId, p.Unit, p.ValidFrom });
    }
}
