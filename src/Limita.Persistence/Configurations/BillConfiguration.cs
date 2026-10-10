using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Limita.Domain.Entities;

namespace Limita.Persistence.Configurations;

public class BillConfiguration : IEntityTypeConfiguration<Bill>
{
    public void Configure(EntityTypeBuilder<Bill> builder)
    {
        builder.HasKey(b => b.Id);

        builder.Property(b => b.BillingNumber)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(b => b.Amount)
            .HasPrecision(18, 2);

        builder.Property(b => b.RowVersion)
            .IsRowVersion();

        builder.HasOne(b => b.Provider)
            .WithMany(p => p.Bills)
            .HasForeignKey(b => b.ProviderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(b => b.User)
            .WithMany(u => u.Bills)
            .HasForeignKey(b => b.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
