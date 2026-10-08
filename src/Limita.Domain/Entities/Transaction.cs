using Limita.Domain.Common;
using Limita.Domain.Enums;

namespace Limita.Domain.Entities;

public class Transaction : BaseEntity
{
    public Guid AccountId { get; set; }
    public Account? Account { get; set; }

    public string ReferenceNumber { get; set; } = string.Empty;
    public TransactionType Type { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "EGP";
    public TransactionStatus Status { get; set; } = TransactionStatus.Completed;
    public string? Description { get; set; }
}
