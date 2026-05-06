using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Domain.Enums;

namespace SureBudgetRequest.Web.Services;

/// <summary>
/// Circuit-scoped (Scoped in Blazor Server = one per SignalR circuit) implementation of ICurrentUser.
/// The active user is set by MainLayout during OnInitializedAsync via IHttpContextAccessor.
/// For development: the cookie "dev_user_id" overrides the default.
/// </summary>
public sealed class CurrentUserService : ICurrentUser
{
    public const string CookieName = "dev_user_id";

    private Guid _userId;
    private string _fullName = "Unknown";
    private string _username = "unknown";
    private UserRole _role = UserRole.Employee;
    private Guid _departmentId;

    public Guid UserId => _userId;
    public string FullName => _fullName;
    public string Username => _username;
    public UserRole Role => _role;
    public Guid DepartmentId => _departmentId;
    public bool IsLoaded { get; private set; }

    public void SetUser(Guid userId, string fullName, string username, UserRole role, Guid departmentId)
    {
        _userId = userId;
        _fullName = fullName;
        _username = username;
        _role = role;
        _departmentId = departmentId;
        IsLoaded = true;
    }
}
