using Limita.Domain.Common;

namespace Limita.Domain.Entities;

public class Withdrawal : BaseEntity
{
    public Guid AccountId { get; set; }
    public Account? Account { get; set; }

    public decimal Amount { get; set; }
    public string CashCode { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsClaimed { get; set; }
}
