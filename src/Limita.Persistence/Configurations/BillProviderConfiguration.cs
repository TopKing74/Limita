using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Limita.Domain.Entities;

namespace Limita.Persistence.Configurations;

public class BillProviderConfiguration : IEntityTypeConfiguration<BillProvider>
{
    public void Configure(EntityTypeBuilder<BillProvider> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasMany(p => p.Bills)
            .WithOne(b => b.Provider)
            .HasForeignKey(b => b.ProviderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
