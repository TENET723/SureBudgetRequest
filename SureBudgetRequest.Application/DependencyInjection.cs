using Microsoft.Extensions.DependencyInjection;

namespace SureBudgetRequest.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers all Application layer services.
    /// Call this from Program.cs: builder.Services.AddApplication()
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));

        return services;
    }
}
