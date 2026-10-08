using Limita.Domain.Common;

namespace Limita.Domain.Entities;

public class ExchangeRate : BaseEntity
{
    public string FromCurrency { get; set; } = string.Empty;
    public string ToCurrency { get; set; } = string.Empty;
    public decimal Rate { get; set; }
    public new DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
