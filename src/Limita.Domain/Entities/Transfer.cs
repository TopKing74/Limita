using Limita.Domain.Common;
using Limita.Domain.Enums;

namespace Limita.Domain.Entities;

public class Transfer : BaseEntity
{
    public Guid TransactionId { get; set; }
    public Transaction? Transaction { get; set; }

    public Guid SourceAccountId { get; set; }
    public Account? SourceAccount { get; set; }

    public Guid DestinationAccountId { get; set; }
    public Account? DestinationAccount { get; set; }

    public decimal Amount { get; set; }
    public TransferStatus Status { get; set; } = TransferStatus.Pending;
}
