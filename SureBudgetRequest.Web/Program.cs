using Microsoft.EntityFrameworkCore;
using SureBudgetRequest.Application;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Infrastructure;
using SureBudgetRequest.Infrastructure.Persistence;
using SureBudgetRequest.Infrastructure.Seeding;
using SureBudgetRequest.Web.Components;
using SureBudgetRequest.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Services ──────────────────────────────────────────────────────────────────
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Dev impersonation: circuit-scoped current-user service
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentUserService>();
builder.Services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<CurrentUserService>());

var app = builder.Build();

// ── Startup tasks: migrate + seed ─────────────────────────────────────────────
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // Apply any pending EF Core migrations automatically on startup.
    // For production, prefer running migrations as a separate deployment step.
    await db.Database.MigrateAsync();

    // Seed development data (no-op if data already exists)
    if (app.Environment.IsDevelopment())
    {
        var seeder = scope.ServiceProvider.GetRequiredService<DbSeeder>();
        await seeder.SeedAsync();
    }
}

// ── Middleware pipeline ────────────────────────────────────────────────────────
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
