using SureBudgetRequest.Domain.Entities;

namespace SureBudgetRequest.Application.Abstractions.Repositories;

public interface ITagRepository
{
    Task<Tag?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Tag?> GetByNameAndDepartmentAsync(string name, Guid departmentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Tag>> ListByDepartmentAsync(
        Guid departmentId,
        bool includeInactive = false,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Tag>> ListAllActiveAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Tag tag, CancellationToken cancellationToken = default);
}
