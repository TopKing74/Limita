using Microsoft.EntityFrameworkCore;
using Limita.Application.Common;
using Limita.Application.Common.Interfaces;
using Limita.Application.Features.Transactions.Models;
using Limita.Domain.Enums;

namespace Limita.Application.Features.Transactions.Services;

/// <summary>
/// Business logic service for retrieving ledger transactions, statements, and financial summaries.
/// Enforces user-level account ownership boundaries on all operations.
/// </summary>
public class TransactionService : ITransactionService
{
    private readonly IApplicationDbContext _context;

    public TransactionService(IApplicationDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<PagedResult<TransactionDto>> GetTransactionsAsync(
        string userId,
        TransactionFilterRequest filter,
        CancellationToken cancellationToken = default)
    {
        // 1. Identify all account IDs belonging to the authenticated user
        var userAccountIds = await _context.Accounts
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);

        if (userAccountIds.Count == 0)
        {
            return new PagedResult<TransactionDto>(
                Array.Empty<TransactionDto>(),
                0,
                filter.PageNumber,
                filter.PageSize);
        }

        // 2. Base query filtered strictly to the user's accounts
        var query = _context.Transactions
            .AsNoTracking()
            .Include(t => t.Account)
            .Where(t => userAccountIds.Contains(t.AccountId));

        // 3. Apply optional account filter (if specified and owned by user)
        if (filter.AccountId.HasValue)
        {
            if (!userAccountIds.Contains(filter.AccountId.Value))
            {
                // Account does not belong to the user; return empty result securely
                return new PagedResult<TransactionDto>(
                    Array.Empty<TransactionDto>(),
                    0,
                    filter.PageNumber,
                    filter.PageSize);
            }

            query = query.Where(t => t.AccountId == filter.AccountId.Value);
        }

        // 4. Apply optional type filter
        if (filter.Type.HasValue)
        {
            query = query.Where(t => t.Type == filter.Type.Value);
        }

        // 5. Apply optional status filter
        if (filter.Status.HasValue)
        {
            query = query.Where(t => t.Status == filter.Status.Value);
        }

        // 6. Apply date range filters (inclusive)
        if (filter.FromDate.HasValue)
        {
            var fromUtc = filter.FromDate.Value.ToUniversalTime();
            query = query.Where(t => t.CreatedAt >= fromUtc);
        }

        if (filter.ToDate.HasValue)
        {
            var toUtc = filter.ToDate.Value.ToUniversalTime();
            query = query.Where(t => t.CreatedAt <= toUtc);
        }

        // 7. Calculate total matching records
        var totalCount = await query.CountAsync(cancellationToken);

        // 8. Order by CreatedAt descending (optimally served by the composite index IX_Transactions_AccountId_CreatedAt)
        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(t => new TransactionDto(
                t.Id,
                t.AccountId,
                t.Account != null ? t.Account.AccountNumber : string.Empty,
                t.ReferenceNumber,
                t.Type,
                t.Type.ToString(),
                t.Amount,
                t.Currency,
                t.Status,
                t.Status.ToString(),
                t.Description,
                t.CreatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<TransactionDto>(items, totalCount, filter.PageNumber, filter.PageSize);
    }

    /// <inheritdoc />
    public async Task<TransactionDetailsDto?> GetTransactionByIdAsync(
        string userId,
        Guid transactionId,
        CancellationToken cancellationToken = default)
    {
        var transaction = await _context.Transactions
            .AsNoTracking()
            .Include(t => t.Account)
            .FirstOrDefaultAsync(
                t => t.Id == transactionId && t.Account != null && t.Account.UserId == userId,
                cancellationToken);

        if (transaction == null || transaction.Account == null)
        {
            return null;
        }

        return new TransactionDetailsDto(
            transaction.Id,
            transaction.AccountId,
            transaction.Account.AccountNumber,
            transaction.Account.Type.ToString(),
            transaction.ReferenceNumber,
            transaction.Type,
            transaction.Type.ToString(),
            transaction.Amount,
            transaction.Currency,
            transaction.Status,
            transaction.Status.ToString(),
            transaction.Description,
            transaction.CreatedAt,
            transaction.UpdatedAt);
    }

    /// <inheritdoc />
    public async Task<TransactionSummaryDto> GetTransactionsSummaryAsync(
        string userId,
        Guid? accountId,
        CancellationToken cancellationToken = default)
    {
        // 1. Identify user accounts
        var userAccounts = await _context.Accounts
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .Select(a => new { a.Id, a.Currency })
            .ToListAsync(cancellationToken);

        var userAccountIds = userAccounts.Select(a => a.Id).ToList();

        // 2. Validate accountId filter if supplied: verify ownership or reject
        if (accountId.HasValue && !userAccountIds.Contains(accountId.Value))
        {
            throw new KeyNotFoundException($"Account '{accountId.Value}' was not found or you do not have access to it.");
        }

        var primaryAccount = accountId.HasValue
            ? userAccounts.FirstOrDefault(a => a.Id == accountId.Value)
            : userAccounts.FirstOrDefault();

        var primaryCurrency = primaryAccount?.Currency ?? "EGP";

        if (userAccountIds.Count == 0)
        {
            return new TransactionSummaryDto(
                TotalInflow: 0m,
                TotalOutflow: 0m,
                NetCashFlow: 0m,
                TotalTransactionsCount: 0,
                Currency: primaryCurrency,
                Breakdown: Array.Empty<TransactionCategoryBreakdownDto>(),
                Currencies: Array.Empty<CurrencySummaryDto>());
        }

        // 3. Base query for completed transactions
        var query = _context.Transactions
            .AsNoTracking()
            .Where(t => userAccountIds.Contains(t.AccountId) && t.Status == TransactionStatus.Completed);

        if (accountId.HasValue)
        {
            query = query.Where(t => t.AccountId == accountId.Value);
        }

        var completedTransactions = await query
            .Select(t => new { t.Type, t.Amount, t.Currency })
            .ToListAsync(cancellationToken);

        // 4. Compute summaries grouped strictly by currency code to avoid cross-currency aggregation
        var currencyGroups = completedTransactions
            .GroupBy(t => string.IsNullOrWhiteSpace(t.Currency) ? primaryCurrency : t.Currency)
            .ToList();

        var byCurrencyList = new List<CurrencySummaryDto>();

        foreach (var group in currencyGroups)
        {
            var currencyCode = group.Key;
            decimal inflow = 0m;
            decimal outflow = 0m;

            foreach (var tx in group)
            {
                switch (tx.Type)
                {
                    case TransactionType.Deposit:
                    case TransactionType.TransferIn:
                        inflow += tx.Amount;
                        break;
                    case TransactionType.Withdrawal:
                    case TransactionType.TransferOut:
                    case TransactionType.BillPayment:
                    case TransactionType.MobileRecharge:
                        outflow += tx.Amount;
                        break;
                }
            }

            var categoryBreakdown = group
                .GroupBy(t => t.Type)
                .Select(g => new TransactionCategoryBreakdownDto(
                    Type: g.Key,
                    CategoryName: g.Key.ToString(),
                    TotalAmount: g.Sum(x => x.Amount),
                    TransactionCount: g.Count()))
                .OrderByDescending(b => b.TotalAmount)
                .ToList();

            byCurrencyList.Add(new CurrencySummaryDto(
                Currency: currencyCode,
                TotalInflow: inflow,
                TotalOutflow: outflow,
                NetCashFlow: inflow - outflow,
                TotalTransactionsCount: group.Count(),
                Breakdown: categoryBreakdown));
        }

        // 5. Select primary currency summary for top-level figures (strictly avoiding multi-currency summation)
        var primarySummary = byCurrencyList.FirstOrDefault(c => c.Currency == primaryCurrency)
            ?? byCurrencyList.FirstOrDefault();

        var topLevelInflow = primarySummary?.TotalInflow ?? 0m;
        var topLevelOutflow = primarySummary?.TotalOutflow ?? 0m;
        var topLevelNet = primarySummary?.NetCashFlow ?? 0m;
        var topLevelCount = primarySummary?.TotalTransactionsCount ?? 0;
        var topLevelBreakdown = primarySummary?.Breakdown ?? (IReadOnlyList<TransactionCategoryBreakdownDto>)Array.Empty<TransactionCategoryBreakdownDto>();
        var topLevelCurrency = primarySummary?.Currency ?? primaryCurrency;

        return new TransactionSummaryDto(
            TotalInflow: topLevelInflow,
            TotalOutflow: topLevelOutflow,
            NetCashFlow: topLevelNet,
            TotalTransactionsCount: topLevelCount,
            Currency: topLevelCurrency,
            Breakdown: topLevelBreakdown,
            Currencies: byCurrencyList);
    }
}
