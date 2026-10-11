using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Limita.Application.Common.Interfaces;
using Limita.Domain.Common;
using Limita.Domain.Entities;

namespace Limita.Persistence.Context;

public class LimitaDbContext : IdentityDbContext<ApplicationUser>, IApplicationDbContext
{
    public LimitaDbContext(DbContextOptions<LimitaDbContext> options)
        : base(options)
    {
    }

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<BankCard> BankCards => Set<BankCard>();
    public DbSet<Beneficiary> Beneficiaries => Set<Beneficiary>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Transfer> Transfers => Set<Transfer>();
    public DbSet<BillProvider> BillProviders => Set<BillProvider>();
    public DbSet<Bill> Bills => Set<Bill>();
    public DbSet<MobileRecharge> MobileRecharges => Set<MobileRecharge>();
    public DbSet<Withdrawal> Withdrawals => Set<Withdrawal>();
    public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();
    public DbSet<Notification> Notifications { get; set; } = null!;

    public override DatabaseFacade Database => base.Database;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(LimitaDbContext).Assembly);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var entries = ChangeTracker.Entries<BaseEntity>();

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = DateTime.UtcNow;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = DateTime.UtcNow;
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}
