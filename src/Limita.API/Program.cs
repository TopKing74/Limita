using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Limita.API.Extensions;
using Limita.API.Middlewares;
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
builder.Services.AddLimitaServices(builder.Configuration);

var app = builder.Build();

// 3. Global Exception Handling Middleware
app.UseMiddleware<ExceptionHandlingMiddleware>();

// 4. Configure Swagger and API Documentation
if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
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

// 5. Middleware Pipeline
app.UseHttpsRedirection();
app.UseCors("LimitaCorsPolicy");
app.UseAuthentication();
app.UseAuthorization();

// 6. Map Controllers and Hubs
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
        logger.LogError(ex, "An error occurred while migrating or seeding the database: {Message}", ex.Message);
    }
}

await app.RunAsync();

public partial class Program { }
