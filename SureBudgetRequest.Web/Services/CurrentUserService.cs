using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Domain.Enums;

namespace SureBudgetRequest.Web.Services;

/// <summary>
/// Resolves the current user from cookie claims set at login.
/// Replaces the old dev-impersonation cookie scheme entirely.
///
/// <para>
/// Dual-source principal resolution:
/// — When an <see cref="HttpContext"/> is available (minimal API endpoints such
///   as attachment downloads, or SSR prerendering), the principal comes straight
///   from <c>HttpContext.User</c>. Same cookie, same claims.
/// — Otherwise (interactive SignalR circuit, where HttpContext is unavailable),
///   it falls back to <see cref="AuthenticationStateProvider"/>.
/// Without the HttpContext path, ServerAuthenticationStateProvider throws
/// "Do not call GetAuthenticationStateAsync outside of the DI scope for a Razor
/// component" when ICurrentUser is touched from a plain HTTP request — e.g. via
/// ScopedMediator bridging the user into an operation scope.
/// </para>
/// </summary>
public sealed class CurrentUserService : ICurrentUser
{
    // Claim keys for fields that aren't on the standard ClaimTypes list.
    public const string ClaimDepartmentId = "asure_dept_id";
    public const string ClaimUsername = "asure_username";
    public const string ClaimMustChangePassword = "asure_must_change_password";
    public const string ClaimIsFinanceApprover = "asure_is_finance_approver";

    private readonly AuthenticationStateProvider _authStateProvider;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private ClaimsPrincipal? _principal;

    public CurrentUserService(
        AuthenticationStateProvider authStateProvider,
        IHttpContextAccessor httpContextAccessor)
    {
        _authStateProvider = authStateProvider;
        _httpContextAccessor = httpContextAccessor;
    }

    // Prefer HttpContext.User when a request is in flight (API endpoints, SSR
    // prerender). Fall back to the circuit's AuthenticationStateProvider, which
    // holds the state captured at circuit start and returns it synchronously
    // thereafter — calling .Result is safe in that context. Cached for the
    // lifetime of the scope.
    private ClaimsPrincipal Principal => _principal ??=
        _httpContextAccessor.HttpContext?.User
        ?? _authStateProvider.GetAuthenticationStateAsync().GetAwaiter().GetResult().User;

    public Guid UserId
    {
        get
        {
            var v = Principal.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(v, out var id) ? id : Guid.Empty;
        }
    }

    public string FullName => Principal.FindFirstValue(ClaimTypes.Name) ?? "";

    public string Username => Principal.FindFirstValue(ClaimUsername) ?? "";

    public UserRole Role
    {
        get
        {
            var v = Principal.FindFirstValue(ClaimTypes.Role);
            return Enum.TryParse<UserRole>(v, out var r) ? r : UserRole.Employee;
        }
    }

    public Guid DepartmentId
    {
        get
        {
            var v = Principal.FindFirstValue(ClaimDepartmentId);
            return Guid.TryParse(v, out var id) ? id : Guid.Empty;
        }
    }

    public bool IsFinanceApprover
    {
        get
        {
            // Defensive: only meaningful for Finance users; never trust the
            // claim alone — gate on Role too so a stale or tampered claim
            // can't grant approver rights to a non-Finance account.
            if (Role != UserRole.Finance) return false;
            var v = Principal.FindFirstValue(ClaimIsFinanceApprover);
            return string.Equals(v, "true", StringComparison.OrdinalIgnoreCase);
        }
    }

    public bool IsLoaded => Principal.Identity?.IsAuthenticated == true;
}
