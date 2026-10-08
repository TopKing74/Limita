using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Limita.Domain.Entities;

namespace Limita.Persistence.Configurations;

public class WithdrawalConfiguration : IEntityTypeConfiguration<Withdrawal>
{
    public void Configure(EntityTypeBuilder<Withdrawal> builder)
    {
        builder.HasKey(w => w.Id);

        builder.Property(w => w.Amount)
            .HasPrecision(18, 2);

        builder.Property(w => w.CashCode)
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(w => w.CashCode)
            .IsUnique();

        builder.HasOne(w => w.Account)
            .WithMany()
            .HasForeignKey(w => w.AccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
