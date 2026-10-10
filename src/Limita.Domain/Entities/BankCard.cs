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
    public CardForm Form { get; set; } = CardForm.Virtual;
    public CardStatus Status { get; set; } = CardStatus.Active;
    public bool IsDefault { get; set; }

    public decimal DailyAtmLimit { get; set; } = 5000m;
    public decimal DailyPosLimit { get; set; } = 20000m;

    /// <summary>PBKDF2 hash of the ATM PIN. Never expose via API.</summary>
    public string? PinHash { get; set; }
    public bool IsPinSet => !string.IsNullOrEmpty(PinHash);

    public CardBlockType? BlockType { get; set; }
    public string? BlockReason { get; set; }
    public DateTime? BlockedAt { get; set; }
}
