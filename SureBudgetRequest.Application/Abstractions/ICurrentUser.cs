using SureBudgetRequest.Domain.Enums;

namespace SureBudgetRequest.Application.Abstractions;

/// <summary>
/// Provides the identity of the user acting in the current request/circuit.
/// Backed by a dev-impersonation cookie in the Web layer.
/// </summary>
public interface ICurrentUser
{
    Guid UserId { get; }
    string FullName { get; }
    string Username { get; }
    UserRole Role { get; }
    Guid DepartmentId { get; }

    /// <summary>
    /// True when the current user is a Finance user with approver rights
    /// (can Approve / Reject / Send Back). False for Finance "payer-only" users.
    /// Always false for non-Finance roles.
    /// </summary>
    bool IsFinanceApprover { get; }

    /// <summary>True once the service has been initialised with an actual user.</summary>
    bool IsLoaded { get; }
}
