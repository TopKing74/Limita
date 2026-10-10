using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Limita.Domain.Entities;

namespace Limita.Persistence.Configurations;

public class BankCardConfiguration : IEntityTypeConfiguration<BankCard>
{
    public void Configure(EntityTypeBuilder<BankCard> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.CardHolderName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(c => c.MaskedNumber)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.Type).IsRequired();
        builder.Property(c => c.Form).IsRequired();
        builder.Property(c => c.Status).IsRequired();

        builder.Property(c => c.DailyAtmLimit)
            .HasPrecision(18, 2);

        builder.Property(c => c.DailyPosLimit)
            .HasPrecision(18, 2);

        builder.Property(c => c.PinHash)
            .HasMaxLength(500);

        builder.Property(c => c.BlockReason)
            .HasMaxLength(250);

        builder.Ignore(c => c.IsPinSet);

        builder.HasIndex(c => c.AccountId);

        builder.HasOne(c => c.Account)
            .WithMany(a => a.Cards)
            .HasForeignKey(c => c.AccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
