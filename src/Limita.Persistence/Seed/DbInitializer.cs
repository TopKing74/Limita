using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Limita.Domain.Entities;
using Limita.Domain.Enums;
using Limita.Persistence.Context;

namespace Limita.Persistence.Seed;

public static class DbInitializer
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<LimitaDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        await SeedAsync(context, userManager, roleManager);
    }

    public static async Task SeedAsync(
        LimitaDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        // 1. Roles
        string[] roles = ["Admin", "Customer"];
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        // 2. Demo Users
        var adminEmail = "admin@limita.bank";
        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser == null)
        {
            adminUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FullName = "Limita Administrator",
                EmailConfirmed = true,
                PhoneNumber = "+201000000001",
                CreatedAt = DateTime.UtcNow
            };
            var result = await userManager.CreateAsync(adminUser, "Admin@123456!");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
            }
        }

        var demoEmail = "john.doe@limita.bank";
        var demoUser = await userManager.FindByEmailAsync(demoEmail);
        if (demoUser == null)
        {
            demoUser = new ApplicationUser
            {
                UserName = demoEmail,
                Email = demoEmail,
                FullName = "John Doe",
                EmailConfirmed = true,
                PhoneNumber = "+201000000002",
                CreatedAt = DateTime.UtcNow
            };
            var result = await userManager.CreateAsync(demoUser, "User@123456!");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(demoUser, "Customer");
            }
        }

        // 3. Default Currencies / Exchange Rates
        if (!await context.ExchangeRates.AnyAsync())
        {
            var rates = new List<ExchangeRate>
            {
                new() { FromCurrency = "USD", ToCurrency = "EGP", Rate = 48.500000m, UpdatedAt = DateTime.UtcNow },
                new() { FromCurrency = "EGP", ToCurrency = "USD", Rate = 0.020619m, UpdatedAt = DateTime.UtcNow },
                new() { FromCurrency = "EUR", ToCurrency = "EGP", Rate = 52.800000m, UpdatedAt = DateTime.UtcNow },
                new() { FromCurrency = "EGP", ToCurrency = "EUR", Rate = 0.018939m, UpdatedAt = DateTime.UtcNow }
            };

            await context.ExchangeRates.AddRangeAsync(rates);
            await context.SaveChangesAsync();
        }

        // 4. Test Accounts for demo user
        if (demoUser != null && !await context.Accounts.AnyAsync(a => a.UserId == demoUser.Id))
        {
            var egpAccount = new Account
            {
                UserId = demoUser.Id,
                AccountNumber = "EG10002000300040",
                Type = AccountType.Checking,
                Currency = "EGP",
                Balance = 100000.00m,
                DailyLimit = 50000.00m,
                MonthlyLimit = 250000.00m,
                Status = AccountStatus.Active
            };

            var usdAccount = new Account
            {
                UserId = demoUser.Id,
                AccountNumber = "US10002000300099",
                Type = AccountType.Savings,
                Currency = "USD",
                Balance = 5000.00m,
                DailyLimit = 2000.00m,
                MonthlyLimit = 10000.00m,
                Status = AccountStatus.Active
            };

            await context.Accounts.AddRangeAsync(egpAccount, usdAccount);
            await context.SaveChangesAsync();

            // 5. Sample Cards
            var debitCard = new BankCard
            {
                AccountId = egpAccount.Id,
                CardHolderName = "JOHN DOE",
                MaskedNumber = "4000 **** **** 1234",
                ExpiryMonth = 12,
                ExpiryYear = DateTime.UtcNow.Year + 3,
                Type = CardType.Debit,
                Status = CardStatus.Active,
                IsDefault = true
            };

            var creditCard = new BankCard
            {
                AccountId = egpAccount.Id,
                CardHolderName = "JOHN DOE",
                MaskedNumber = "5100 **** **** 5678",
                ExpiryMonth = 6,
                ExpiryYear = DateTime.UtcNow.Year + 2,
                Type = CardType.Credit,
                Status = CardStatus.Active,
                IsDefault = false
            };

            await context.BankCards.AddRangeAsync(debitCard, creditCard);
            await context.SaveChangesAsync();
        }

        // 6. Bill Providers
        var demoBillProviders = new List<BillProvider>
        {
            new()
            {
                Id = Guid.Parse("a11c1001-b111-4000-8000-000000000001"),
                Name = "Orange Telecom Egypt",
                Category = BillCategory.Telecom,
                IsActive = true
            },
            new()
            {
                Id = Guid.Parse("a11c1001-b111-4000-8000-000000000002"),
                Name = "South Cairo Electricity Distribution",
                Category = BillCategory.Electricity,
                IsActive = true
            },
            new()
            {
                Id = Guid.Parse("a11c1001-b111-4000-8000-000000000003"),
                Name = "Cairo Water Company",
                Category = BillCategory.Water,
                IsActive = true
            },
            new()
            {
                Id = Guid.Parse("a11c1001-b111-4000-8000-000000000004"),
                Name = "WE Internet Egypt",
                Category = BillCategory.Internet,
                IsActive = true
            },
            new()
            {
                Id = Guid.Parse("a11c1001-b111-4000-8000-000000000005"),
                Name = "Egypt Gas Company",
                Category = BillCategory.Gas,
                IsActive = true
            },
            new()
            {
                Id = Guid.Parse("a11c1001-b111-4000-8000-000000000006"),
                Name = "Egyptian Tax Authority",
                Category = BillCategory.Government,
                IsActive = true
            }
        };

        var existingProviderIds = await context.BillProviders
            .Select(p => p.Id)
            .ToListAsync();
        var existingProviderNames = await context.BillProviders
            .Select(p => p.Name)
            .ToListAsync();

        var providersToInsert = demoBillProviders
            .Where(p => !existingProviderIds.Contains(p.Id) && !existingProviderNames.Contains(p.Name))
            .ToList();

        if (providersToInsert.Count > 0)
        {
            await context.BillProviders.AddRangeAsync(providersToInsert);
            await context.SaveChangesAsync();
        }

        // 7. Pending Bills
        if (!await context.Bills.AnyAsync())
        {
            var demoProviderNames = demoBillProviders.Select(p => p.Name).ToList();
            var providerIdsByName = await context.BillProviders
                .Where(p => demoProviderNames.Contains(p.Name))
                .ToDictionaryAsync(p => p.Name, p => p.Id);

            var bills = new List<Bill>();

            if (providerIdsByName.TryGetValue("South Cairo Electricity Distribution", out var electricityId))
            {
                bills.Add(new Bill
                {
                    ProviderId = electricityId,
                    BillingNumber = "ELEC-2026-98124",
                    Amount = 850.50m,
                    DueDate = DateTime.UtcNow.AddDays(14),
                    Status = BillStatus.Pending
                });
            }

            if (providerIdsByName.TryGetValue("WE Internet Egypt", out var internetId))
            {
                bills.Add(new Bill
                {
                    ProviderId = internetId,
                    BillingNumber = "INT-2026-55412",
                    Amount = 320.00m,
                    DueDate = DateTime.UtcNow.AddDays(7),
                    Status = BillStatus.Pending
                });
            }

            if (providerIdsByName.TryGetValue("Cairo Water Company", out var waterId))
            {
                bills.Add(new Bill
                {
                    ProviderId = waterId,
                    BillingNumber = "WTR-2026-11234",
                    Amount = 145.75m,
                    DueDate = DateTime.UtcNow.AddDays(21),
                    Status = BillStatus.Pending
                });
            }

            if (bills.Count > 0)
            {
                await context.Bills.AddRangeAsync(bills);
                await context.SaveChangesAsync();
            }
        }
    }
}
