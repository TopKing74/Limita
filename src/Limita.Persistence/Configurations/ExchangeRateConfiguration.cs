using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Limita.Domain.Entities;

namespace Limita.Persistence.Configurations;

public class ExchangeRateConfiguration : IEntityTypeConfiguration<ExchangeRate>
{
    public void Configure(EntityTypeBuilder<ExchangeRate> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.FromCurrency)
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(e => e.ToCurrency)
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(e => e.Rate)
            .HasPrecision(18, 6);

        builder.HasIndex(e => new { e.FromCurrency, e.ToCurrency })
            .IsUnique();
    }
}
