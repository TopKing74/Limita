namespace Limita.Application.Features.Beneficiaries.Models;

/// <summary>
/// Request payload for saving a new transfer beneficiary (contact) to the user's directory.
/// </summary>
/// <param name="FullName">The full legal name or display name of the recipient.</param>
/// <param name="AccountNumber">The recipient's bank account number or IBAN.</param>
/// <param name="BankName">The recipient's bank name (e.g., CIB, NBE, QNB, Limita Bank).</param>
/// <param name="PhoneNumber">Optional contact phone number for the recipient.</param>
/// <param name="IsFavorite">Flag indicating whether this contact is pinned as a favorite for quick transfers.</param>
public record CreateBeneficiaryRequest(
    string FullName,
    string AccountNumber,
    string BankName,
    string? PhoneNumber = null,
    bool IsFavorite = false);

/// <summary>
/// Request payload for updating an existing saved beneficiary.
/// Note: AccountNumber is immutable once created to preserve financial audit consistency.
/// </summary>
/// <param name="FullName">Updated full name or nickname for the recipient.</param>
/// <param name="BankName">Updated bank name.</param>
/// <param name="PhoneNumber">Updated contact phone number.</param>
/// <param name="IsFavorite">Updated favorite status flag.</param>
public record UpdateBeneficiaryRequest(
    string FullName,
    string BankName,
    string? PhoneNumber = null,
    bool IsFavorite = false);

/// <summary>
/// Data Transfer Object representing a saved beneficiary returned to client applications.
/// </summary>
/// <param name="Id">Unique identifier of the beneficiary record.</param>
/// <param name="FullName">Full name of the recipient.</param>
/// <param name="AccountNumber">Masked or full account number / IBAN.</param>
/// <param name="BankName">Name of the recipient's financial institution.</param>
/// <param name="PhoneNumber">Recipient's contact phone number.</param>
/// <param name="IsFavorite">Indicates whether the beneficiary is marked as favorite.</param>
/// <param name="CreatedAt">UTC timestamp when the contact was initially saved.</param>
/// <param name="UpdatedAt">UTC timestamp when the contact was last modified.</param>
public record BeneficiaryDto(
    Guid Id,
    string FullName,
    string AccountNumber,
    string BankName,
    string? PhoneNumber,
    bool IsFavorite,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
