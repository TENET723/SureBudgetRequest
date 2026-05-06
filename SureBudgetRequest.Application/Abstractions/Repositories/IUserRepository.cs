using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;

namespace SureBudgetRequest.Application.Abstractions.Repositories;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Returns the single company-wide Boss, or null if none is assigned.</summary>
    Task<User?> FindBossAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<User>> ListAsync(
        Guid? departmentId = null,
        UserRole? role = null,
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    Task AddAsync(User user, CancellationToken cancellationToken = default);
}
