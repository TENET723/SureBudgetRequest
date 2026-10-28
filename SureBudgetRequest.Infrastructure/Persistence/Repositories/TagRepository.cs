using Microsoft.EntityFrameworkCore;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Entities;

namespace SureBudgetRequest.Infrastructure.Persistence.Repositories;

public class TagRepository : ITagRepository
{
    private readonly AppDbContext _context;

    public TagRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Tag?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Tags.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<Tag?> GetByNameAndDepartmentAsync(string name, Guid departmentId, CancellationToken cancellationToken = default)
    {
        var trimmedLower = name.Trim().ToLower();
        return await _context.Tags
            .FirstOrDefaultAsync(t => t.DepartmentId == departmentId && t.Name.ToLower() == trimmedLower, cancellationToken);
    }

    public async Task<IReadOnlyList<Tag>> ListByDepartmentAsync(
        Guid departmentId,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Tags.Where(t => t.DepartmentId == departmentId);

        if (!includeInactive)
        {
            query = query.Where(t => t.IsActive);
        }

        return await query.OrderBy(t => t.Name).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Tag>> ListAllActiveAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Tags
            .AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Tag tag, CancellationToken cancellationToken = default)
    {
        await _context.Tags.AddAsync(tag, cancellationToken);
    }
}
