using DentaCore.Application.Interfaces;
using DentaCore.Infrastructure.Identity;
using DentaCore.Infrastructure.Interceptors;
using DentaCore.Infrastructure.Persistence;
using DentaCore.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuestPDF.Infrastructure;

namespace DentaCore.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // QuestPDF Community License
        QuestPDF.Settings.License = LicenseType.Community;

        // Register interceptors
        services.AddScoped<AuditLogSaveChangesInterceptor>();

        // Database Configuration: PostgreSQL with automatic SQLite fallback
        var providerSetting = configuration["DATABASE_PROVIDER"] ?? configuration["Database:Provider"];
        var rawPg = configuration["DATABASE_URL"] ?? configuration["POSTGRES_CONNECTION"] ?? configuration.GetConnectionString("PostgreSQL");
        var rawSqlite = configuration["SQLITE_CONNECTION"] ?? configuration["DATABASE_FALLBACK_URL"] ?? configuration.GetConnectionString("SQLite") ?? "Data Source=dentacore.db";

        var postgresConn = DatabaseConnectionHelper.ToPostgreSqlConnectionString(rawPg);
        var sqliteConn = DatabaseConnectionHelper.NormalizeSqliteConnectionString(rawSqlite);

        bool usePostgres = false;
        bool isExplicitSqlite = string.Equals(providerSetting, "SQLite", StringComparison.OrdinalIgnoreCase);

        if (!isExplicitSqlite && (!string.IsNullOrWhiteSpace(rawPg) || string.Equals(providerSetting, "PostgreSQL", StringComparison.OrdinalIgnoreCase)))
        {
            Console.WriteLine("[Database] Testing connection to PostgreSQL...");
            if (DatabaseConnectionHelper.CanConnectToPostgreSql(postgresConn, out var error, timeoutSeconds: 3))
            {
                usePostgres = true;
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("[Database] Successfully connected to PostgreSQL. Active provider: PostgreSQL");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[Database] PostgreSQL is unavailable ({error}). Auto-falling back to SQLite fallback: {sqliteConn}");
                Console.ResetColor();
            }
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[Database] Active provider: SQLite ({sqliteConn})");
            Console.ResetColor();
        }

        services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            var interceptor = sp.GetRequiredService<AuditLogSaveChangesInterceptor>();
            options.AddInterceptors(interceptor);

            if (usePostgres)
            {
                options.UseNpgsql(postgresConn);
            }
            else
            {
                options.UseSqlite(sqliteConn);
            }
        });

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        // Identity & Security
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();

        // Services
        services.AddScoped<IPdfReceiptService, QuestPdfReceiptService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<DatabaseSeeder>();

        return services;
    }
}
