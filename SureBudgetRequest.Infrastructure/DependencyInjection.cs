using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.Abstractions.Security;
using SureBudgetRequest.Application.Abstractions.Services;
using SureBudgetRequest.Infrastructure.Notifications;
using SureBudgetRequest.Infrastructure.Persistence;
using SureBudgetRequest.Infrastructure.Persistence.Repositories;
using SureBudgetRequest.Infrastructure.Security;
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
        services.AddScoped<ICoaRepository, CoaRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // ── Security ──────────────────────────────────────────────────────────
        // Singleton: stateless and thread-safe.
        services.AddSingleton<IPasswordHasher, PasswordHasher>();

        // ── Notifications (outbox pattern) ────────────────────────────────────
        services.Configure<SlackOptions>(configuration.GetSection(SlackOptions.SectionName));
        services.AddScoped<INotificationService, SlackNotificationService>();
        services.AddHttpClient<NotificationOutboxProcessor>();
        services.AddHostedService<NotificationOutboxProcessor>();

        // ── File Storage ──────────────────────────────────────────────────────
        // Provider is selected by the "Storage:Provider" config value.
        //   "Supabase" (default) → SupabaseFileStorage
        //   "Local"              → LocalFileStorage  (dev fallback)
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
        services.Configure<SupabaseStorageOptions>(configuration.GetSection(SupabaseStorageOptions.SectionName));

        var storageProvider = configuration.GetValue<string>("Storage:Provider") ?? "Supabase";

        if (string.Equals(storageProvider, "Local", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<IFileStorage, LocalFileStorage>();
        }
        else
        {
            // Supabase storage talks REST. Use a named HttpClient with the project URL
            // as BaseAddress and the service-role key as a bearer token on every call.
            services.AddHttpClient("SupabaseStorage", (sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptions<SupabaseStorageOptions>>().Value;
                if (string.IsNullOrWhiteSpace(opts.Url))
                    throw new InvalidOperationException(
                        "Supabase:Url is missing. Add the 'Supabase' section to appsettings.json.");
                if (string.IsNullOrWhiteSpace(opts.ServiceRoleKey))
                    throw new InvalidOperationException(
                        "Supabase:ServiceRoleKey is missing. Add the 'Supabase' section to appsettings.json.");

                client.BaseAddress = new Uri(opts.Url.TrimEnd('/') + "/");
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", opts.ServiceRoleKey);
                // Supabase also looks at the apikey header for some calls.
                client.DefaultRequestHeaders.Add("apikey", opts.ServiceRoleKey);
            });

            services.AddScoped<IFileStorage>(sp =>
            {
                var factory = sp.GetRequiredService<IHttpClientFactory>();
                var http = factory.CreateClient("SupabaseStorage");
                var opts = sp.GetRequiredService<IOptions<SupabaseStorageOptions>>();
                return new SupabaseFileStorage(http, opts);
            });
        }

        // ── Seeder (registered so Program.cs can resolve and call it) ─────────
        services.AddScoped<DbSeeder>();

        return services;
    }
}
