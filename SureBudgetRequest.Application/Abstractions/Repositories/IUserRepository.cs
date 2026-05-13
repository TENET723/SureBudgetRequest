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

    Task<IReadOnlyList<User>> ListAsync(
        Guid? departmentId = null,
        UserRole? role = null,
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    Task AddAsync(User user, CancellationToken cancellationToken = default);
}
