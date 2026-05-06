using Microsoft.EntityFrameworkCore;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;

namespace SureBudgetRequest.Infrastructure.Persistence.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly AppDbContext _context;

    public UserRepository(AppDbContext context) => _context = context;

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Users.FindAsync([id], cancellationToken);

    public async Task<User?> FindBossAsync(CancellationToken cancellationToken = default)
        => await _context.Users
            .Where(u => u.Role == UserRole.Boss && u.IsActive)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<User>> ListAsync(
        Guid? departmentId = null,
        UserRole? role = null,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Users.AsQueryable();

        if (!includeInactive)
            query = query.Where(u => u.IsActive);

        if (departmentId.HasValue)
            query = query.Where(u => u.DepartmentId == departmentId.Value);

        if (role.HasValue)
            query = query.Where(u => u.Role == role.Value);

        return await query
            .OrderBy(u => u.FullName)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
        => await _context.Users.AddAsync(user, cancellationToken);
}
