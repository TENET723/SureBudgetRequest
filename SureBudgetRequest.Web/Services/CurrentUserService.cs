using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Domain.Enums;

namespace SureBudgetRequest.Web.Services;

/// <summary>
/// Resolves the current user from cookie claims set at login.
/// Replaces the old dev-impersonation cookie scheme entirely.
/// </summary>
public sealed class CurrentUserService : ICurrentUser
{
    // Claim keys for fields that aren't on the standard ClaimTypes list.
    public const string ClaimDepartmentId = "asure_dept_id";
    public const string ClaimUsername = "asure_username";
    public const string ClaimMustChangePassword = "asure_must_change_password";

    private readonly AuthenticationStateProvider _authStateProvider;
    private ClaimsPrincipal? _principal;

    public CurrentUserService(AuthenticationStateProvider authStateProvider)
    {
        _authStateProvider = authStateProvider;
    }

    // In Blazor Server, AuthenticationStateProvider holds the state captured at
    // circuit start and returns it synchronously thereafter. Calling .Result here
    // is safe in that context. Cached for the lifetime of the scope (one circuit).
    private ClaimsPrincipal Principal => _principal ??=
        _authStateProvider.GetAuthenticationStateAsync().GetAwaiter().GetResult().User;

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

    public bool IsLoaded => Principal.Identity?.IsAuthenticated == true;
}
