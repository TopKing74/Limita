using Limita.Domain.Common;
using Limita.Domain.Enums;

namespace Limita.Domain.Entities;

public class BankCard : BaseEntity
{
    public Guid AccountId { get; set; }
    public Account? Account { get; set; }

    public string CardHolderName { get; set; } = string.Empty;
    public string MaskedNumber { get; set; } = string.Empty;
    public int ExpiryMonth { get; set; }
    public int ExpiryYear { get; set; }
    public CardType Type { get; set; } = CardType.Debit;
    public CardStatus Status { get; set; } = CardStatus.Active;
    public bool IsDefault { get; set; }
}
