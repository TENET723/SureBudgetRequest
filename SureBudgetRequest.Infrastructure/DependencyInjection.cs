using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.Abstractions.Services;
using SureBudgetRequest.Infrastructure.Notifications;
using SureBudgetRequest.Infrastructure.Persistence;
using SureBudgetRequest.Infrastructure.Persistence.Repositories;
using SureBudgetRequest.Infrastructure.Seeding;
using SureBudgetRequest.Infrastructure.Storage;

namespace SureBudgetRequest.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers all Infrastructure services.
    /// Call from Program.cs: builder.Services.AddInfrastructure(builder.Configuration)
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── Database ──────────────────────────────────────────────────────────
        var connectionString = configuration.GetConnectionString("Supabase")
            ?? throw new InvalidOperationException(
                "Connection string 'Supabase' is missing from configuration.");

        services.AddDbContext<AppDbContext>(options =>
            options
                .UseNpgsql(connectionString, npgsql =>
                {
                    npgsql.EnableRetryOnFailure(maxRetryCount: 3);
                })
                .UseSnakeCaseNamingConvention());

        // ── Repositories & Unit of Work ───────────────────────────────────────
        services.AddScoped<IBudgetRequestRepository, BudgetRequestRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IDepartmentRepository, DepartmentRepository>();
        services.AddScoped<ICurrencyRepository, CurrencyRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // ── Notifications (outbox pattern) ────────────────────────────────────
        services.Configure<SlackOptions>(configuration.GetSection(SlackOptions.SectionName));

        // SlackNotificationService is scoped — it writes to the scoped AppDbContext
        // in the same transaction as the domain command.
        services.AddScoped<INotificationService, SlackNotificationService>();

        // The outbox processor is a singleton BackgroundService.
        // It creates its own scopes to avoid capturing scoped DbContext.
        services.AddHttpClient<NotificationOutboxProcessor>();
        services.AddHostedService<NotificationOutboxProcessor>();

        // ── File Storage ──────────────────────────────────────────────────────
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
        services.AddScoped<IFileStorage, LocalFileStorage>();

        // ── Seeder (registered so Program.cs can resolve and call it) ─────────
        services.AddScoped<DbSeeder>();

        return services;
    }
}
