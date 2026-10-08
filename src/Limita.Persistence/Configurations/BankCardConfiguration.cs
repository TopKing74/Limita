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

        builder.HasOne(c => c.Account)
            .WithMany(a => a.Cards)
            .HasForeignKey(c => c.AccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
