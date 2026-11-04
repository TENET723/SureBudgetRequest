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

    /// <summary>
    /// True when the current user is currently assigned as the head of a department.
    /// </summary>
    bool IsDepartmentHead { get; }

    /// <summary>
    /// True when the current session is acting as a fallback person on behalf of someone on leave.
    /// </summary>
    bool IsFallbackPersonAct { get; }

    /// <summary>
    /// The actual logged-in user ID when in fallback act mode; otherwise equal to <see cref="UserId"/>.
    /// </summary>
    Guid RealActorUserId { get; }

    /// <summary>
    /// The actual logged-in user's full name when in fallback act mode; otherwise equal to <see cref="FullName"/>.
    /// </summary>
    string RealActorFullName { get; }

    /// <summary>True once the service has been initialised with an actual user.</summary>
    bool IsLoaded { get; }
}
