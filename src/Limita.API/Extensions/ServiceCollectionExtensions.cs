using System.Text;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Limita.Application.Common;
using Limita.Application.Common.Interfaces;
using Limita.Application.Features.Accounts.Services;
using Limita.Application.Features.Auth.Services;
using Limita.Application.Features.Users.Services;
using Limita.Application.Features.Beneficiaries.Services;
using Limita.Application.Features.Transactions.Services;
using Limita.Application.Features.Transfers.Services;
using Limita.Application.Features.Bills.Services;
using Limita.Application.Features.Cards.Services;
using Limita.Application.Features.Recharge.Services;
using Limita.Application.Features.Notifications.Services;
using Limita.Domain.Entities;
using Limita.Infrastructure.Authentication;
using Limita.Infrastructure.Hubs;
using Limita.Infrastructure.Identity;
using Limita.Infrastructure.Notifications;
using Limita.Infrastructure.Security;
using Limita.Persistence.Context;
using Microsoft.AspNetCore.SignalR;

namespace Limita.API.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLimitaServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDatabase(configuration);
        services.AddIdentityServices();
        services.AddJwtAuthentication(configuration);
        services.AddApplicationDependencies();
        services.AddCorsPolicy();
        services.AddSwaggerDocumentation();

        return services;
    }

    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found in configuration.");

        services.AddDbContext<LimitaDbContext>(options =>
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.MigrationsAssembly(typeof(LimitaDbContext).Assembly.FullName);
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorNumbersToAdd: null);
            }));

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<LimitaDbContext>());

        return services;
    }

    public static IServiceCollection AddIdentityServices(this IServiceCollection services)
    {
        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
        {
            // Password policy
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequiredLength = 8;
            options.Password.RequiredUniqueChars = 1;

            // Lockout policy
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.AllowedForNewUsers = true;

            // User policy
            options.User.RequireUniqueEmail = true;
        })
        .AddEntityFrameworkStores<LimitaDbContext>()
        .AddDefaultTokenProviders();

        return services;
    }

    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSection = configuration.GetSection(JwtOptions.SectionName);
        services.Configure<JwtOptions>(jwtSection);

        var jwtOptions = jwtSection.Get<JwtOptions>() ?? new JwtOptions
        {
            Issuer = "LimitaBankingServer",
            Audience = "LimitaBankingClient",
            SecretKey = "LimitaSuperSecretSecureKeyForBankingApplication2026!#*$",
            ExpiryMinutes = 60,
            RefreshTokenExpiryDays = 7
        };

        var key = Encoding.UTF8.GetBytes(jwtOptions.SecretKey);

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.RequireHttpsMetadata = false;
            options.SaveToken = true;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = jwtOptions.Issuer,
                ValidateAudience = true,
                ValidAudience = jwtOptions.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            options.MapInboundClaims = false;
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;
                    if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    {
                        context.Token = accessToken;
                    }

                    return Task.CompletedTask;
                },
                OnAuthenticationFailed = context =>
                {
                    var logger = context.HttpContext.RequestServices
                        .GetRequiredService<ILoggerFactory>()
                        .CreateLogger("JwtBearer");
                    logger.LogWarning(context.Exception,
                        "JWT authentication failed: {Message}", context.Exception.Message);
                    return Task.CompletedTask;
                },
                OnChallenge = async context =>
                {
                    context.HandleResponse();
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    context.Response.ContentType = "application/json";

                    var response = ApiResponse.Fail("Unauthorized: Invalid or missing authentication token.");
                    var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });

                    await context.Response.WriteAsync(json);
                },
                OnForbidden = async context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    context.Response.ContentType = "application/json";

                    var response = ApiResponse.Fail("Forbidden: You do not possess the required permissions.");
                    var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });

                    await context.Response.WriteAsync(json);
                }
            };
        });

        services.AddAuthorization();

        return services;
    }

    public static IServiceCollection AddApplicationDependencies(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IBeneficiaryService, BeneficiaryService>();
        services.AddScoped<ITransactionService, TransactionService>();
        services.AddScoped<ITransferService, TransferService>();
        services.AddScoped<ICardService, CardService>();
        services.AddSingleton<ICardPinHasher, CardPinHasher>();
        services.AddScoped<IBillService, BillService>();
        services.AddScoped<IMobileRechargeService, MobileRechargeService>();
        services.AddSignalR();
        services.AddSingleton<IUserIdProvider, CustomUserIdProvider>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddValidatorsFromAssembly(typeof(ApiResponse<>).Assembly);

        return services;
    }

    public static IServiceCollection AddCorsPolicy(this IServiceCollection services)
    {
        services.AddCors(options =>
        {
            options.AddPolicy("LimitaCorsPolicy", builder =>
            {
                builder.SetIsOriginAllowed(_ => true)
                       .AllowAnyMethod()
                       .AllowAnyHeader()
                       .AllowCredentials();
            });
        });

        return services;
    }

    public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Limita Digital Banking API",
                Version = "v1",
                Description = "High-performance Digital Banking REST API built with .NET 9 and Lean Clean Architecture.",
                Contact = new OpenApiContact
                {
                    Name = "Limita Architecture Team",
                    Email = "architecture@limita.bank"
                }
            });

            // Http + Bearer: Swagger UI auto-prefixes "Bearer " — paste accessToken only.
            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Description = "Paste the JWT accessToken only (from /api/Auth/login). Do not type the word Bearer.",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "Bearer",
                BearerFormat = "JWT"
            });

            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });

            c.CustomSchemaIds(type => type.FullName?.Replace("+", "."));
        });

        return services;
    }
}
