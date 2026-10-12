using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Limita.Application.Common;
using Limita.Application.Common.Interfaces;
using Limita.Application.Features.Auth.Models;
using Limita.Domain.Entities;
using Limita.Persistence.Context;
using Limita.Persistence.Seed;

namespace Limita.IntegrationTests.Infrastructure;

public class LimitaTestFactory : WebApplicationFactory<Program>
{
    public const string TestConnectionString =
        "Server=(localdb)\\mssqllocaldb;Database=LimitaDb_IntegrationTests;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public Mock<IEmailService> EmailServiceMock { get; } = new();

    private static readonly SemaphoreSlim _initLock = new(1, 1);
    private static bool _globalInitialized;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            // Remove existing DbContext registrations
            var descriptorsToRemove = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<LimitaDbContext>) ||
                d.ServiceType == typeof(DbContextOptions) ||
                d.ServiceType == typeof(LimitaDbContext) ||
                d.ServiceType == typeof(IApplicationDbContext)).ToList();

            foreach (var descriptor in descriptorsToRemove)
            {
                services.Remove(descriptor);
            }

            // Register LimitaDbContext with dedicated isolated test database
            services.AddDbContext<LimitaDbContext>(options =>
                options.UseSqlServer(TestConnectionString, sqlOptions =>
                {
                    sqlOptions.MigrationsAssembly(typeof(LimitaDbContext).Assembly.FullName);
                    sqlOptions.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null);
                }));

            services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<LimitaDbContext>());

            // Replace real EmailService with safe mock
            var emailDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IEmailService));
            if (emailDescriptor != null)
            {
                services.Remove(emailDescriptor);
            }
            services.AddSingleton(EmailServiceMock.Object);
        });
    }

    public async Task EnsureDatabaseInitializedAsync()
    {
        if (_globalInitialized) return;

        await _initLock.WaitAsync();
        try
        {
            if (_globalInitialized) return;

            using var scope = Services.CreateScope();
            var sp = scope.ServiceProvider;
            var context = sp.GetRequiredService<LimitaDbContext>();

            await context.Database.MigrateAsync();
            await DbInitializer.SeedAsync(sp);

            var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
            var john = await userManager.FindByEmailAsync("john.doe@limita.bank");
            if (john != null)
            {
                var token = await userManager.GeneratePasswordResetTokenAsync(john);
                await userManager.ResetPasswordAsync(john, token, "User@123456!");
                await userManager.SetLockoutEndDateAsync(john, null);
                await userManager.ResetAccessFailedCountAsync(john);
            }

            var admin = await userManager.FindByEmailAsync("admin@limita.bank");
            if (admin != null)
            {
                var token = await userManager.GeneratePasswordResetTokenAsync(admin);
                await userManager.ResetPasswordAsync(admin, token, "Admin@123456!");
                await userManager.SetLockoutEndDateAsync(admin, null);
                await userManager.ResetAccessFailedCountAsync(admin);
            }

            _globalInitialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task ResetUserLockoutAsync(string email = "john.doe@limita.bank")
    {
        await EnsureDatabaseInitializedAsync();
        using var scope = Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        if (user != null)
        {
            await userManager.SetLockoutEndDateAsync(user, null);
            await userManager.ResetAccessFailedCountAsync(user);
        }
    }

    public async Task<string> GetAccessTokenAsync(string email = "john.doe@limita.bank", string password = "User@123456!")
    {
        await EnsureDatabaseInitializedAsync();
        await ResetUserLockoutAsync(email);

        using var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/Auth/login", new LoginRequest(email, password), JsonOptions);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>(JsonOptions);
        return body?.Data?.AccessToken ?? throw new InvalidOperationException("Failed to obtain access token from test login.");
    }

    public HttpClient CreateAuthorizedClient(string token)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
