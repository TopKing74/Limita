using Limita.Domain.Common;
using Limita.Domain.Enums;

namespace Limita.Domain.Entities;

public class Bill : BaseEntity
{
    public Guid ProviderId { get; set; }
    public BillProvider? Provider { get; set; }

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    public string BillingNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime DueDate { get; set; }
    public BillStatus Status { get; set; } = BillStatus.Pending;
}
