using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SureBudgetRequest.Application;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.BudgetRequests.Common;
using SureBudgetRequest.Infrastructure;
using SureBudgetRequest.Infrastructure.Persistence;
using SureBudgetRequest.Infrastructure.Seeding;
using SureBudgetRequest.Web.Components;
using SureBudgetRequest.Web.Endpoints;
using SureBudgetRequest.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Services ──────────────────────────────────────────────────────────────────
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    // Bump the SignalR max message size so the Blazor InputFile component can stream
    // attachments up the SignalR circuit. Default is 32KB which trips file uploads.
    // We size it to match our per-file cap with a small buffer.
    .AddHubOptions(o =>
    {
        o.MaximumReceiveMessageSize = AttachmentConstraints.MaxBytes + 64 * 1024;
    });

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// ── ScopedMediator ─────────────────────────────────────────────────────────────
// Blazor Server: one DI scope per circuit means one AppDbContext per browser
// session, and fast page switching causes concurrent operations on that single
// context ("A second operation was started on this context instance...").
// ScopedMediator runs every Send/Publish in its own DI scope so each command/
// query gets its own DbContext. Must come AFTER AddApplication/AddInfrastructure
// so it replaces MediatR's own IMediator registration.
builder.Services.Replace(ServiceDescriptor.Scoped<IMediator>(sp =>
    ActivatorUtilities.CreateInstance<ScopedMediator>(sp)));

// ── Authentication: cookie scheme ─────────────────────────────────────────────
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = ".SureBudget.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        // Keep SecurePolicy=SameAsRequest in Development so HTTP localhost works.
        // In Production it should be Always (set via env-specific config if needed).
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;

        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
        options.AccessDeniedPath = "/login";

        // 8-hour sliding session — users typically log in once per workday.
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

// ICurrentUser is now resolved from cookie claims via AuthenticationStateProvider.
builder.Services.AddHttpContextAccessor();
// ICurrentUser resolution:
//  - Circuit scope: CurrentUserService (claims via AuthenticationStateProvider).
//  - ScopedMediator operation scopes: CurrentUserSnapshot, populated from the
//    circuit user before the handler runs (no AuthenticationStateProvider there).
builder.Services.AddScoped<CurrentUserService>();
builder.Services.AddScoped<CurrentUserSnapshot>();
builder.Services.AddScoped<ICurrentUser>(sp =>
{
    var snapshot = sp.GetRequiredService<CurrentUserSnapshot>();
    return snapshot.IsSet
        ? snapshot
        : sp.GetRequiredService<CurrentUserService>();
});
builder.Services.AddScoped<SureBudgetRequest.Web.Services.ToastService>();
// Circuit-scoped global busy signal for the full-screen LoadingOverlay.
builder.Services.AddScoped<BusyState>();

var app = builder.Build();

// ── Startup tasks: migrate + seed ─────────────────────────────────────────────
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();

    //if (app.Environment.IsDevelopment())
    //{
    //    var seeder = scope.ServiceProvider.GetRequiredService<DbSeeder>();
    //    await seeder.SeedAsync();
    //}
}

// ── Middleware pipeline ───────────────────────────────────────────────────────
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

//app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseStaticFiles(); // Or app.MapStaticAssets() for .NET 9+

app.UseRouting(); // 1. Add this explicitly before Antiforgery/Auth

app.UseAuthentication(); // 2. Move Authentication BEFORE Antiforgery
app.UseAntiforgery();    // 3. Antiforgery now has access to the User identity
app.UseAuthorization();

// Every Blazor page requires auth by default; pages can opt out with [AllowAnonymous]
// (Login does this). Unauthenticated requests get redirected to /login by the cookie
// auth scheme's LoginPath above.
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .RequireAuthorization();

// Attachment HTTP endpoints (download).
app.MapAttachmentEndpoints();

// Report export HTTP endpoints (xlsx download).
app.MapReportEndpoints();

// Logout endpoint. Form-posted from MainLayout with an antiforgery token.
app.MapPost("/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/login");
});

app.Run();




