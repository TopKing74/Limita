using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Limita.Domain.Entities;

namespace Limita.Persistence.Configurations;

public class BeneficiaryConfiguration : IEntityTypeConfiguration<Beneficiary>
{
    public void Configure(EntityTypeBuilder<Beneficiary> builder)
    {
        builder.HasKey(b => b.Id);

        builder.Property(b => b.OwnerUserId)
            .IsRequired();

        builder.Property(b => b.FullName)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(b => b.AccountNumber)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(b => b.PhoneNumber)
            .HasMaxLength(20);

        builder.Property(b => b.BankName)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasOne(b => b.OwnerUser)
            .WithMany(u => u.Beneficiaries)
            .HasForeignKey(b => b.OwnerUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
