using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Limita.API.Extensions;
using Limita.API.Middlewares;
using Limita.Application.Common;
using Limita.Infrastructure.Hubs;
using Limita.Persistence.Context;
using Limita.Persistence.Seed;

var builder = WebApplication.CreateBuilder(args);

// 1. Add Controllers
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// 2. Add Limita Core, Persistence, Infrastructure, Identity, and Security Services
builder.Services.AddLimitaServices(builder.Configuration, builder.Environment);

// 3. Register ASP.NET Core Built-in Rate Limiting Services
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter(policyName: "AuthLimiter", opt =>
    {
        opt.PermitLimit = 10;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 0;
    });
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.ContentType = "application/json";
        var response = ApiResponse.Fail("Too many requests. Please try again later.");
        await context.HttpContext.Response.WriteAsJsonAsync(response, cancellationToken: token);
    };
});

// 4. Register ProblemDetails
builder.Services.AddProblemDetails();

var app = builder.Build();

// 5. Global Exception Handling Middleware
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";
            var response = ApiResponse.Fail(
                "An internal server error occurred while processing your request.",
                new List<string> { "Please contact support if the issue persists." });
            await context.Response.WriteAsJsonAsync(response);
        });
    });
}
app.UseMiddleware<ExceptionHandlingMiddleware>();

// 6. Configure Swagger and API Documentation (strictly Development only)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Limita Digital Banking API v1");
        c.RoutePrefix = string.Empty; // Serves Swagger UI at application root URL
        c.DocumentTitle = "Limita Banking API Documentation";
        c.DisplayRequestDuration();
    });
}

// 7. Middleware Pipeline
app.UseHttpsRedirection();
app.UseCors("DefaultCorsPolicy");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// 8. Map Controllers and Hubs
app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notifications");

// 7. Automatic Database Migration and Seed Execution on Startup
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        var context = services.GetRequiredService<LimitaDbContext>();
        logger.LogInformation("Checking database connection and applying pending migrations...");

        if (context.Database.IsRelational())
        {
            await context.Database.MigrateAsync();
        }
        else
        {
            await context.Database.EnsureCreatedAsync();
        }

        logger.LogInformation("Executing database seeder for demo users and financial data...");
        await DbInitializer.SeedAsync(services);
        logger.LogInformation("Database migration and seeding completed successfully.");
    }
    catch (Exception ex)
    {
        logger.LogCritical(
            ex,
            "Database migration or seeding failed. The application cannot start.");

        throw;
    }
}

await app.RunAsync();

public partial class Program { }
