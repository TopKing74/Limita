using Limita.Domain.Enums;

namespace Limita.Application.Features.Accounts.Models;

public record CreateAccountRequest(
    AccountType Type,
    string Currency = "EGP");

public record AccountSummaryDto(
    Guid Id,
    string AccountNumber,
    AccountType Type,
    string Currency,
    decimal Balance,
    AccountStatus Status,
    DateTime CreatedAt);

public record AccountDetailsDto(
    Guid Id,
    string AccountNumber,
    AccountType Type,
    string Currency,
    decimal Balance,
    AccountStatus Status,
    decimal DailyLimit,
    decimal MonthlyLimit,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record AccountBalanceDto(
    Guid AccountId,
    string AccountNumber,
    string Currency,
    decimal Balance,
    AccountStatus Status,
    DateTime AsOfUtc);
