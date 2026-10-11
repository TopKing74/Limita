using Limita.Domain.Common;
using Limita.Domain.Enums;

namespace Limita.Domain.Entities;

public class Account : BaseEntity
{
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    public string AccountNumber { get; set; } = string.Empty;
    public AccountType Type { get; set; } = AccountType.Checking;
    public string Currency { get; set; } = "EGP";
    public decimal Balance { get; set; }
    public AccountStatus Status { get; set; } = AccountStatus.Active;
    public decimal PerTransactionLimit { get; set; } = 50000m;
    public decimal DailyLimit { get; set; } = 50000m;
    public decimal MonthlyLimit { get; set; } = 250000m;
    public byte[] RowVersion { get; set; } = [];

    public ICollection<BankCard> Cards { get; set; } = new List<BankCard>();
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
