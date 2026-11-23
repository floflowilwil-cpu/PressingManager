using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PressingManager.Core.Entities;

namespace PressingManager.Infrastructure.Data.Configurations;

public sealed class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.ToTable("Expenses");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Category).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Amount).IsRequired();
        builder.Property(e => e.Comment).HasMaxLength(500);
        builder.Property(e => e.CancelReason).HasMaxLength(500);

        builder.HasOne(e => e.CashSession)
            .WithMany(c => c.Expenses)
            .HasForeignKey(e => e.CashSessionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.Date);
        builder.HasIndex(e => e.Category);
    }
}
