using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;

namespace SureBudgetRequest.Application.Abstractions.Repositories;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Looks up a user by email (case-insensitive). Returns inactive users too;
    /// the caller is responsible for checking <c>IsActive</c>.</summary>
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>True if a user with the given email already exists (case-insensitive).</summary>
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generic list with optional filters.
    /// <paramref name="isFinanceApprover"/> is most commonly used with
    /// <paramref name="role"/> = <see cref="UserRole.Finance"/> to find users
    /// who can act at the Finance approval stage.
    /// </summary>
    Task<IReadOnlyList<User>> ListAsync(
        Guid? departmentId = null,
        UserRole? role = null,
        bool includeInactive = false,
        bool? isFinanceApprover = null,
        CancellationToken cancellationToken = default);

    Task AddAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts active Finance users with <c>IsFinanceApprover = true</c>.
    /// Used by the bus-factor safeguard to prevent removing the last approver.
    /// </summary>
    Task<int> CountActiveFinanceApproversAsync(CancellationToken cancellationToken = default);
}
