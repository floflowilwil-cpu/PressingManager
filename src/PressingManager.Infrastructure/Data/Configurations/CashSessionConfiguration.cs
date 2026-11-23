using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PressingManager.Core.Entities;

namespace PressingManager.Infrastructure.Data.Configurations;

public sealed class CashSessionConfiguration : IEntityTypeConfiguration<CashSession>
{
    public void Configure(EntityTypeBuilder<CashSession> builder)
    {
        builder.ToTable("CashSessions");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.OpeningAmount).IsRequired();
        builder.Property(c => c.CloseComment).HasMaxLength(500);

        builder.HasIndex(c => c.Status);
        builder.HasIndex(c => c.OpenedAt);
    }
}
