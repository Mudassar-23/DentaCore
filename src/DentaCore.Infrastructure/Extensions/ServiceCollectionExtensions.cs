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

        // Database Configuration: PostgreSQL with SQLite fallback
        var provider = configuration["DATABASE_PROVIDER"] ?? configuration["Database:Provider"] ?? "SQLite";

        services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            var interceptor = sp.GetRequiredService<AuditLogSaveChangesInterceptor>();
            options.AddInterceptors(interceptor);

            if (string.Equals(provider, "PostgreSQL", StringComparison.OrdinalIgnoreCase))
            {
                var postgresConn = configuration["POSTGRES_CONNECTION"]
                                ?? configuration.GetConnectionString("PostgreSQL")
                                ?? "Host=localhost;Port=5432;Database=dentacore_db;Username=dentacore_user;Password=YourStrongPassword123!;";
                options.UseNpgsql(postgresConn);
            }
            else
            {
                var sqliteConn = configuration["SQLITE_CONNECTION"]
                              ?? configuration.GetConnectionString("SQLite")
                              ?? "Data Source=dentacore.db";
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
