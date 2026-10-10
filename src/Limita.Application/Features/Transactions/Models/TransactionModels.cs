using Limita.Domain.Enums;

namespace Limita.Application.Features.Transactions.Models;

/// <summary>
/// Query parameters for filtering and paginating transaction ledger records.
/// </summary>
public record TransactionFilterRequest
{
    /// <summary>
    /// Optional account identifier. If omitted, transactions across all user accounts are retrieved.
    /// </summary>
    public Guid? AccountId { get; init; }

    /// <summary>
    /// Optional transaction type filter (e.g. Deposit, Withdrawal, TransferIn, TransferOut, BillPayment, MobileRecharge).
    /// </summary>
    public TransactionType? Type { get; init; }

    /// <summary>
    /// Optional transaction status filter (e.g. Pending, Completed, Failed, Reversed).
    /// </summary>
    public TransactionStatus? Status { get; init; }

    /// <summary>
    /// Optional start date (inclusive) for filtering transactions.
    /// </summary>
    public DateTime? FromDate { get; init; }

    /// <summary>
    /// Optional end date (inclusive) for filtering transactions.
    /// </summary>
    public DateTime? ToDate { get; init; }

    /// <summary>
    /// 1-based page number for pagination. Defaults to 1.
    /// </summary>
    public int PageNumber { get; init; } = 1;

    /// <summary>
    /// Number of transactions per page (maximum 50). Defaults to 10.
    /// </summary>
    public int PageSize { get; init; } = 10;
}

/// <summary>
/// Data Transfer Object representing a single transaction in list views and statements.
/// </summary>
/// <param name="Id">Unique identifier of the transaction.</param>
/// <param name="AccountId">Identifier of the linked account.</param>
/// <param name="AccountNumber">Bank account number for display.</param>
/// <param name="ReferenceNumber">Unique bank transaction reference code.</param>
/// <param name="Type">Numerical transaction type enum value.</param>
/// <param name="TypeName">Human-readable transaction type name.</param>
/// <param name="Amount">Transaction amount.</param>
/// <param name="Currency">Currency code (e.g. EGP).</param>
/// <param name="Status">Numerical status enum value.</param>
/// <param name="StatusName">Human-readable transaction status name.</param>
/// <param name="Description">Narrative or description of the transaction.</param>
/// <param name="CreatedAt">UTC timestamp when the transaction was executed.</param>
public record TransactionDto(
    Guid Id,
    Guid AccountId,
    string AccountNumber,
    string ReferenceNumber,
    TransactionType Type,
    string TypeName,
    decimal Amount,
    string Currency,
    TransactionStatus Status,
    string StatusName,
    string? Description,
    DateTime CreatedAt);

/// <summary>
/// Detailed transaction receipt object for a specific ledger entry.
/// </summary>
/// <param name="Id">Unique identifier of the transaction.</param>
/// <param name="AccountId">Identifier of the linked account.</param>
/// <param name="AccountNumber">Bank account number.</param>
/// <param name="AccountType">Type of account (e.g. Checking, Savings).</param>
/// <param name="ReferenceNumber">Unique bank transaction reference code.</param>
/// <param name="Type">Numerical transaction type enum value.</param>
/// <param name="TypeName">Human-readable transaction type name.</param>
/// <param name="Amount">Transaction amount.</param>
/// <param name="Currency">Currency code.</param>
/// <param name="Status">Numerical status enum value.</param>
/// <param name="StatusName">Human-readable transaction status name.</param>
/// <param name="Description">Narrative description of the transaction.</param>
/// <param name="CreatedAt">UTC timestamp when the transaction was created.</param>
/// <param name="UpdatedAt">UTC timestamp when the transaction was last updated.</param>
public record TransactionDetailsDto(
    Guid Id,
    Guid AccountId,
    string AccountNumber,
    string AccountType,
    string ReferenceNumber,
    TransactionType Type,
    string TypeName,
    decimal Amount,
    string Currency,
    TransactionStatus Status,
    string StatusName,
    string? Description,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// <summary>
/// Breakdown summary item for a specific transaction category.
/// </summary>
/// <param name="Type">Transaction type.</param>
/// <param name="CategoryName">Human-readable category name.</param>
/// <param name="TotalAmount">Total volume transacted under this category.</param>
/// <param name="TransactionCount">Total number of transactions in this category.</param>
public record TransactionCategoryBreakdownDto(
    TransactionType Type,
    string CategoryName,
    decimal TotalAmount,
    int TransactionCount);

/// <summary>
/// Aggregated financial summary grouped for a specific currency code.
/// </summary>
public record CurrencySummaryDto(
    string Currency,
    decimal TotalInflow,
    decimal TotalOutflow,
    decimal NetCashFlow,
    int TotalTransactionsCount,
    IReadOnlyList<TransactionCategoryBreakdownDto> Breakdown);

/// <summary>
/// Aggregated financial summary of the user's transactions (total inflow, outflow, and breakdown).
/// Supports multi-currency portfolios grouped per currency code.
/// </summary>
/// <param name="TotalInflow">Total deposits and incoming transfers for the primary currency.</param>
/// <param name="TotalOutflow">Total withdrawals, outgoing transfers, and payments for the primary currency.</param>
/// <param name="NetCashFlow">Net cash flow (Inflow minus Outflow) for the primary currency.</param>
/// <param name="TotalTransactionsCount">Total number of transactions executed in the primary currency.</param>
/// <param name="Currency">Primary currency code (e.g. EGP).</param>
/// <param name="Breakdown">List of categorized volume breakdowns for the primary currency.</param>
/// <param name="Currencies">Per-currency breakdown list for multi-currency user portfolios.</param>
public record TransactionSummaryDto(
    decimal TotalInflow,
    decimal TotalOutflow,
    decimal NetCashFlow,
    int TotalTransactionsCount,
    string Currency,
    IReadOnlyList<TransactionCategoryBreakdownDto> Breakdown,
    IReadOnlyList<CurrencySummaryDto>? Currencies = null)
{
    public IReadOnlyList<CurrencySummaryDto> ByCurrency => Currencies ?? Array.Empty<CurrencySummaryDto>();
}
