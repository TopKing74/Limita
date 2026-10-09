using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Limita.Domain.Entities;

namespace Limita.Persistence.Configurations;

public class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.AccountNumber)
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(a => a.AccountNumber)
            .IsUnique();

        builder.Property(a => a.Type)
            .IsRequired();

        builder.Property(a => a.Currency)
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(a => a.Balance)
            .HasPrecision(18, 2);

        builder.Property(a => a.DailyLimit)
            .HasPrecision(18, 2);

        builder.Property(a => a.MonthlyLimit)
            .HasPrecision(18, 2);

        builder.Property(a => a.RowVersion)
            .IsRowVersion();

        builder.HasOne(a => a.User)
            .WithMany(u => u.Accounts)
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(a => a.Cards)
            .WithOne(c => c.Account)
            .HasForeignKey(c => c.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(a => a.Transactions)
            .WithOne(t => t.Account)
            .HasForeignKey(t => t.AccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
