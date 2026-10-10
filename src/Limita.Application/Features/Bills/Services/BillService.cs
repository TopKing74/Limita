using Microsoft.EntityFrameworkCore;
using Limita.Application.Common.Interfaces;
using Limita.Application.Features.Bills.Models;
using Limita.Domain.Entities;
using Limita.Domain.Enums;

namespace Limita.Application.Features.Bills.Services;

public class BillService : IBillService
{
    private readonly IApplicationDbContext _context;

    public BillService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<BillDto> CreateBillAsync(string userId, CreateBillRequest request, CancellationToken cancellationToken = default)
    {
        // Verify provider exists and is active
        var provider = await _context.BillProviders
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.ProviderId, cancellationToken);

        if (provider == null)
            throw new InvalidOperationException("The specified Bill Provider does not exist.");

        if (!provider.IsActive)
            throw new InvalidOperationException("The specified Bill Provider is currently inactive.");

        var bill = new Bill
        {
            UserId = userId,
            ProviderId = request.ProviderId,
            BillingNumber = request.BillingNumber,
            Amount = request.Amount,
            DueDate = request.DueDate,
            Status = BillStatus.Pending
        };

        _context.Bills.Add(bill);
        await _context.SaveChangesAsync(cancellationToken);

        return new BillDto(
            bill.Id,
            bill.ProviderId,
            provider.Name,
            bill.BillingNumber,
            bill.Amount,
            bill.DueDate,
            bill.Status,
            bill.CreatedAt
        );
    }

    public async Task<IEnumerable<BillDto>> GetUserBillsAsync(string userId, Guid? providerId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Bills
            .AsNoTracking()
            .Include(b => b.Provider)
            .Where(b => b.UserId == userId);

        if (providerId.HasValue)
        {
            query = query.Where(b => b.ProviderId == providerId.Value);
        }

        var bills = await query.OrderByDescending(b => b.CreatedAt).ToListAsync(cancellationToken);

        return bills.Select(b => new BillDto(
            b.Id,
            b.ProviderId,
            b.Provider?.Name ?? string.Empty,
            b.BillingNumber,
            b.Amount,
            b.DueDate,
            b.Status,
            b.CreatedAt
        ));
    }

    public async Task<BillPaymentResponseDto> PayBillAsync(string userId, Guid billId, PayBillRequest request, CancellationToken cancellationToken = default)
    {
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? dbTransaction = null;
        try
        {
            if (_context.Database != null && _context.Database.CurrentTransaction == null)
            {
                dbTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            }
        }
        catch (InvalidOperationException) { }
        catch (NotSupportedException) { }

        try
        {
            var bill = await _context.Bills
                .FirstOrDefaultAsync(b => b.Id == billId && b.UserId == userId, cancellationToken);

            if (bill == null)
                throw new KeyNotFoundException("Bill not found or you do not have access to it.");

            if (bill.Status == BillStatus.Paid)
                throw new InvalidOperationException("The bill is already paid.");

            if (bill.Status != BillStatus.Pending)
                throw new InvalidOperationException($"The bill cannot be paid because its status is {bill.Status}.");

            var account = await _context.Accounts
                .FirstOrDefaultAsync(a => a.Id == request.AccountId && a.UserId == userId, cancellationToken);

            if (account == null)
                throw new KeyNotFoundException("Account not found or you do not have access to it.");

            if (account.Status != AccountStatus.Active)
                throw new InvalidOperationException("Cannot pay from an inactive or blocked account.");

            if (account.Balance < bill.Amount)
                throw new InvalidOperationException("Insufficient available balance.");

            // Debit the account
            account.Balance -= bill.Amount;
            account.UpdatedAt = DateTime.UtcNow;

            // Transition bill status atomically
            bill.Status = BillStatus.Paid;
            bill.UpdatedAt = DateTime.UtcNow;

            // Create ledger transaction
            var transaction = new Transaction
            {
                AccountId = account.Id,
                ReferenceNumber = $"BPY-{Guid.NewGuid():N}"[..16].ToUpperInvariant(),
                Type = TransactionType.BillPayment,
                Amount = bill.Amount,
                Currency = account.Currency,
                Status = TransactionStatus.Completed,
                Description = $"Payment for bill {bill.BillingNumber}",
                CreatedAt = DateTime.UtcNow
            };

            await _context.Transactions.AddAsync(transaction, cancellationToken);

            try
            {
                // Atomically persist changes
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new InvalidOperationException("The account balance or bill status was modified by another transaction. Please try again.");
            }

            if (dbTransaction != null)
            {
                await dbTransaction.CommitAsync(cancellationToken);
            }

            return new BillPaymentResponseDto(
                bill.Id,
                transaction.Id,
                transaction.ReferenceNumber,
                bill.Amount,
                DateTime.UtcNow
            );
        }
        catch
        {
            if (dbTransaction != null)
            {
                await dbTransaction.RollbackAsync(cancellationToken);
            }
            throw;
        }
        finally
        {
            if (dbTransaction != null)
            {
                await dbTransaction.DisposeAsync();
            }
        }
    }
}
