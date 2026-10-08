using Limita.Domain.Common;

namespace Limita.Domain.Entities;

public class MobileRecharge : BaseEntity
{
    public Guid AccountId { get; set; }
    public Account? Account { get; set; }

    public string PhoneNumber { get; set; } = string.Empty;
    public string OperatorName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string ReferenceNumber { get; set; } = string.Empty;
}
