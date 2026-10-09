using Microsoft.EntityFrameworkCore;
using Limita.Application.Common.Interfaces;
using Limita.Application.Features.Accounts.Models;
using Limita.Domain.Entities;
using Limita.Domain.Enums;

namespace Limita.Application.Features.Accounts.Services;

public class AccountService : IAccountService
{
    private readonly IApplicationDbContext _context;

    public AccountService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<AccountDetailsDto> CreateSecondaryAccountAsync(
        string userId,
        CreateAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        var currency = request.Currency.Trim().ToUpperInvariant();
        var accountNumber = await GenerateUniqueAccountNumberAsync(currency, cancellationToken);

        var (dailyLimit, monthlyLimit) = GetDefaultLimits(request.Type, currency);

        var account = new Account
        {
            UserId = userId,
            AccountNumber = accountNumber,
            Type = request.Type,
            Currency = currency,
            Balance = 0m,
            Status = AccountStatus.Active,
            DailyLimit = dailyLimit,
            MonthlyLimit = monthlyLimit,
            CreatedAt = DateTime.UtcNow
        };

        await _context.Accounts.AddAsync(account, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return MapToDetails(account);
    }

    public async Task<IReadOnlyList<AccountSummaryDto>> GetUserAccountsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Accounts
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new AccountSummaryDto(
                a.Id,
                a.AccountNumber,
                a.Type,
                a.Currency,
                a.Balance,
                a.Status,
                a.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<AccountDetailsDto?> GetAccountByIdAsync(
        string userId,
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        var account = await _context.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == accountId && a.UserId == userId, cancellationToken);

        return account is null ? null : MapToDetails(account);
    }

    public async Task<AccountBalanceDto?> GetAccountBalanceAsync(
        string userId,
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        var account = await _context.Accounts
            .AsNoTracking()
            .Where(a => a.Id == accountId && a.UserId == userId)
            .Select(a => new { a.Id, a.AccountNumber, a.Currency, a.Balance, a.Status })
            .FirstOrDefaultAsync(cancellationToken);

        if (account is null)
        {
            return null;
        }

        return new AccountBalanceDto(
            account.Id,
            account.AccountNumber,
            account.Currency,
            account.Balance,
            account.Status,
            DateTime.UtcNow);
    }

    private async Task<string> GenerateUniqueAccountNumberAsync(
        string currency,
        CancellationToken cancellationToken)
    {
        var prefix = currency switch
        {
            "USD" => "US",
            "EUR" => "EU",
            _ => "EG"
        };

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var candidate = $"{prefix}{DateTime.UtcNow.Ticks.ToString()[^14..]}";
            var exists = await _context.Accounts
                .AnyAsync(a => a.AccountNumber == candidate, cancellationToken);

            if (!exists)
            {
                return candidate;
            }

            await Task.Delay(1, cancellationToken);
        }

        return $"{prefix}{Guid.NewGuid():N}"[..16].ToUpperInvariant();
    }

    private static (decimal DailyLimit, decimal MonthlyLimit) GetDefaultLimits(
        AccountType type,
        string currency)
    {
        return (type, currency) switch
        {
            (AccountType.Savings, "EGP") => (20000m, 100000m),
            (AccountType.Savings, "USD") => (1000m, 5000m),
            (AccountType.Savings, "EUR") => (1000m, 5000m),
            (AccountType.Checking, "USD") => (2000m, 10000m),
            (AccountType.Checking, "EUR") => (2000m, 10000m),
            _ => (50000m, 250000m) // Checking EGP default
        };
    }

    private static AccountDetailsDto MapToDetails(Account account) =>
        new(
            account.Id,
            account.AccountNumber,
            account.Type,
            account.Currency,
            account.Balance,
            account.Status,
            account.DailyLimit,
            account.MonthlyLimit,
            account.CreatedAt,
            account.UpdatedAt);
}
