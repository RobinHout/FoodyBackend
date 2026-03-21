using Microsoft.EntityFrameworkCore;

namespace FoodyBackend;

public static class DatabaseStartupExtensions
{
    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("DatabaseStartup");

        logger.LogInformation("Connecting to MySQL using {ConnectionTarget}", DatabaseConnectionStringFactory.BuildSafeDescription(context.Database.GetConnectionString() ?? string.Empty));

        if (app.Configuration.GetValue("Database:AutoMigrate", true))
        {
            await context.Database.MigrateAsync();
            logger.LogInformation("Database migrations applied successfully.");
            return;
        }

        if (!await context.Database.CanConnectAsync())
        {
            throw new InvalidOperationException("The MySQL database is configured, but the application could not connect to it.");
        }

        logger.LogInformation("Database connection verified successfully.");
    }
}
