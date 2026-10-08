using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Limita.Domain.Entities;

namespace Limita.Persistence.Configurations;

public class MobileRechargeConfiguration : IEntityTypeConfiguration<MobileRecharge>
{
    public void Configure(EntityTypeBuilder<MobileRecharge> builder)
    {
        builder.HasKey(m => m.Id);

        builder.Property(m => m.PhoneNumber)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(m => m.OperatorName)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(m => m.Amount)
            .HasPrecision(18, 2);

        builder.Property(m => m.ReferenceNumber)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(m => m.ReferenceNumber)
            .IsUnique();

        builder.HasOne(m => m.Account)
            .WithMany()
            .HasForeignKey(m => m.AccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
