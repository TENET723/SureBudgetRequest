using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Domain.Enums;

namespace SureBudgetRequest.Web.Services;

/// <summary>
/// A plain, settable <see cref="ICurrentUser"/> used inside per-operation DI scopes
/// created by <see cref="ScopedMediator"/>.
///
/// Why this exists: in Blazor Server the claims-backed <see cref="CurrentUserService"/>
/// depends on the circuit's AuthenticationStateProvider, which is NOT available in a
/// child scope created via IServiceScopeFactory. ScopedMediator therefore copies the
/// circuit user's values into this snapshot before any handler resolves ICurrentUser.
/// </summary>
public sealed class CurrentUserSnapshot : ICurrentUser
{
    /// <summary>True once <see cref="CopyFrom"/> has run (i.e. we are in an operation scope).</summary>
    public bool IsSet { get; private set; }

    public Guid UserId { get; private set; }
    public string FullName { get; private set; } = "";
    public string Username { get; private set; } = "";
    public UserRole Role { get; private set; } = UserRole.Employee;
    public Guid DepartmentId { get; private set; }
    public bool IsFinanceApprover { get; private set; }
    public bool IsLoaded { get; private set; }

    public void CopyFrom(ICurrentUser source)
    {
        UserId = source.UserId;
        FullName = source.FullName;
        Username = source.Username;
        Role = source.Role;
        DepartmentId = source.DepartmentId;
        IsFinanceApprover = source.IsFinanceApprover;
        IsLoaded = source.IsLoaded;
        IsSet = true;
    }
}
