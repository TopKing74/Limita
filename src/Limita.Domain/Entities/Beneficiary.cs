using Limita.Domain.Common;

namespace Limita.Domain.Entities;

public class Beneficiary : BaseEntity
{
    public string OwnerUserId { get; set; } = string.Empty;
    public ApplicationUser? OwnerUser { get; set; }

    public string FullName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public bool IsFavorite { get; set; }
}
