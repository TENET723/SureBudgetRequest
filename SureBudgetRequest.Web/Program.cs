using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
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
builder.Services.AddScoped<ICurrentUser, CurrentUserService>();

var app = builder.Build();

// ── Startup tasks: migrate + seed ─────────────────────────────────────────────
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();

    if (app.Environment.IsDevelopment())
    {
        var seeder = scope.ServiceProvider.GetRequiredService<DbSeeder>();
        await seeder.SeedAsync();
    }
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
