using Microsoft.EntityFrameworkCore;
using Limita.Application.Common.Interfaces;
using Limita.Application.Features.Recharge.Models;
using Limita.Domain.Entities;
using Limita.Domain.Enums;

namespace Limita.Application.Features.Recharge.Services;

public class MobileRechargeService : IMobileRechargeService
{
    private readonly IApplicationDbContext _context;

    public MobileRechargeService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<MobileRechargeResponseDto> RechargeMobileAsync(
        string userId,
        MobileRechargeRequest request,
        CancellationToken cancellationToken = default)
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
            var account = await _context.Accounts
                .FirstOrDefaultAsync(a => a.Id == request.AccountId && a.UserId == userId, cancellationToken);

            if (account is null)
            {
                throw new KeyNotFoundException("Account not found or you do not have access to it.");
            }

            if (account.Status != AccountStatus.Active)
            {
                throw new InvalidOperationException("Cannot perform mobile recharge from an inactive or blocked account.");
            }

            if (account.Balance < request.Amount)
            {
                throw new InvalidOperationException("Insufficient available balance.");
            }

            // Debit account balance
            account.Balance -= request.Amount;
            account.UpdatedAt = DateTime.UtcNow;

            var referenceNumber = $"MRC-{Guid.NewGuid():N}"[..16].ToUpperInvariant();

            // Record MobileRecharge entity
            var recharge = new MobileRecharge
            {
                AccountId = account.Id,
                PhoneNumber = request.PhoneNumber.Trim(),
                OperatorName = request.Operator.ToString(),
                Amount = request.Amount,
                ReferenceNumber = referenceNumber
            };

            await _context.MobileRecharges.AddAsync(recharge, cancellationToken);

            // Record transaction ledger tracking (REC-01)
            var transaction = new Transaction
            {
                AccountId = account.Id,
                ReferenceNumber = referenceNumber,
                Type = TransactionType.MobileRecharge,
                Amount = request.Amount,
                Currency = account.Currency,
                Status = TransactionStatus.Completed,
                Description = $"Mobile recharge for {request.PhoneNumber.Trim()} ({request.Operator})",
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

            if (dbTransaction != null)
            {
                await dbTransaction.CommitAsync(cancellationToken);
            }

            return new MobileRechargeResponseDto(
                recharge.Id,
                account.Id,
                recharge.PhoneNumber,
                recharge.OperatorName,
                recharge.Amount,
                recharge.ReferenceNumber,
                transaction.Id,
                recharge.CreatedAt);
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
