using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Limita.Domain.Entities;

namespace Limita.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<ApplicationUser> Users { get; }
    DbSet<Account> Accounts { get; }
    DbSet<BankCard> BankCards { get; }
    DbSet<Beneficiary> Beneficiaries { get; }
    DbSet<Transaction> Transactions { get; }
    DbSet<Transfer> Transfers { get; }
    DbSet<BillProvider> BillProviders { get; }
    DbSet<Bill> Bills { get; }
    DbSet<MobileRecharge> MobileRecharges { get; }
    DbSet<Withdrawal> Withdrawals { get; }
    DbSet<ExchangeRate> ExchangeRates { get; }
    DbSet<Notification> Notifications { get; }

    DatabaseFacade Database { get; }
    ChangeTracker ChangeTracker { get; }

    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
