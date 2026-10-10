using Microsoft.EntityFrameworkCore;
using Limita.Application.Common.Interfaces;
using Limita.Application.Features.Accounts.Models;
using Limita.Domain.Entities;
using Limita.Domain.Enums;

namespace Limita.Application.Features.Accounts.Services;

public class AccountService : IAccountService
{
    private static int _sequence;
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
        var currency = string.IsNullOrWhiteSpace(request.Currency)
            ? "EGP"
            : request.Currency.Trim().ToUpperInvariant();
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

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsAccountNumberUniqueViolation(ex))
        {
            throw new InvalidOperationException(
                "An account with this account number already exists. Please try again.", ex);
        }

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
            // Combine high-resolution timestamp, monotonic sequence counter, and random entropy to prevent concurrent collisions
            var seq = (Interlocked.Increment(ref _sequence) & 0x7FFFFFFF) % 10000;
            var randomPart = Random.Shared.Next(100, 1000);
            var timestampPart = DateTime.UtcNow.Ticks.ToString()[^7..];
            var candidate = $"{prefix}{timestampPart}{seq:D4}{randomPart:D3}";

            var exists = await _context.Accounts
                .AnyAsync(a => a.AccountNumber == candidate, cancellationToken);

            if (!exists)
            {
                return candidate;
            }

            await Task.Delay(10, cancellationToken);
        }

        return $"{prefix}{Guid.NewGuid():N}"[..16].ToUpperInvariant();
    }

    private static bool IsAccountNumberUniqueViolation(DbUpdateException ex)
    {
        var details = $"{ex.Message} {ex.InnerException?.Message}";

        if (details.Contains("IX_Accounts_AccountNumber", StringComparison.OrdinalIgnoreCase)
            || details.Contains("AccountNumber", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if ((details.Contains("2601") || details.Contains("2627"))
            && ex.Entries.Any(e => e.Entity is Account))
        {
            return true;
        }

        return false;
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

    public async Task<TransactionDto> WithdrawAsync(
        string userId,
        Guid accountId,
        WithdrawalRequest request,
        CancellationToken cancellationToken = default)
    {
        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == accountId && a.UserId == userId, cancellationToken);

        if (account is null)
        {
            throw new KeyNotFoundException("Account not found or you do not have access to it.");
        }

        if (account.Status != AccountStatus.Active)
        {
            throw new InvalidOperationException("Cannot withdraw from an inactive or blocked account.");
        }

        if (account.Balance < request.Amount)
        {
            throw new InvalidOperationException("Insufficient available balance.");
        }

        account.Balance -= request.Amount;

        var transaction = new Transaction
        {
            AccountId = accountId,
            ReferenceNumber = $"WTH-{Guid.NewGuid():N}"[..16].ToUpperInvariant(),
            Type = TransactionType.Withdrawal,
            Amount = request.Amount,
            Currency = account.Currency,
            Status = TransactionStatus.Completed,
            Description = "Cash withdrawal",
            CreatedAt = DateTime.UtcNow
        };

        await _context.Transactions.AddAsync(transaction, cancellationToken);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new InvalidOperationException("The account balance was modified by another transaction. Please try again.");
        }

        return new TransactionDto(
            transaction.Id,
            transaction.AccountId,
            transaction.ReferenceNumber,
            transaction.Type,
            transaction.Amount,
            transaction.Currency,
            transaction.Status,
            transaction.CreatedAt);
    }

    public async Task<TransactionDto> DepositAsync(
        string userId,
        Guid accountId,
        DepositRequest request,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteDepositInternalAsync(userId, accountId, request.Amount, "deposit to", "Account deposit", cancellationToken);
    }

    public async Task<TransactionDto> RechargeAsync(
        string userId,
        Guid accountId,
        RechargeRequest request,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteDepositInternalAsync(userId, accountId, request.Amount, "recharge", "Account recharge", cancellationToken);
    }

    private async Task<TransactionDto> ExecuteDepositInternalAsync(
        string userId,
        Guid accountId,
        decimal amount,
        string actionVerb,
        string description,
        CancellationToken cancellationToken)
    {
        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == accountId && a.UserId == userId, cancellationToken);

        if (account is null)
        {
            throw new KeyNotFoundException("Account not found or you do not have access to it.");
        }

        if (account.Status != AccountStatus.Active)
        {
            throw new InvalidOperationException($"Cannot {actionVerb} an inactive or blocked account.");
        }

        account.Balance += amount;

        var transaction = new Transaction
        {
            AccountId = accountId,
            ReferenceNumber = $"DEP-{Guid.NewGuid():N}"[..16].ToUpperInvariant(),
            Type = TransactionType.Deposit,
            Amount = amount,
            Currency = account.Currency,
            Status = TransactionStatus.Completed,
            Description = description,
            CreatedAt = DateTime.UtcNow
        };

        await _context.Transactions.AddAsync(transaction, cancellationToken);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new InvalidOperationException("The account balance was modified by another transaction. Please try again.");
        }

        return new TransactionDto(
            transaction.Id,
            transaction.AccountId,
            transaction.ReferenceNumber,
            transaction.Type,
            transaction.Amount,
            transaction.Currency,
            transaction.Status,
            transaction.CreatedAt);
    }
}
