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

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        var normalized = email.Trim().ToLowerInvariant();
        return await _context.Users
            .SingleOrDefaultAsync(u => u.Email == normalized, cancellationToken);
    }

    public async Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        var normalized = email.Trim().ToLowerInvariant();
        return await _context.Users
            .AnyAsync(u => u.Email == normalized, cancellationToken);
    }

    public async Task<IReadOnlyList<User>> ListAsync(
        Guid? departmentId = null,
        UserRole? role = null,
        bool includeInactive = false,
        bool? isFinanceApprover = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Users.AsQueryable();

        if (!includeInactive)
            query = query.Where(u => u.IsActive);

        if (departmentId.HasValue)
            query = query.Where(u => u.DepartmentId == departmentId.Value);

        if (role.HasValue)
            query = query.Where(u => u.Role == role.Value);

        if (isFinanceApprover.HasValue)
            query = query.Where(u => u.IsFinanceApprover == isFinanceApprover.Value);

        return await query
            .OrderBy(u => u.FullName)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
        => await _context.Users.AddAsync(user, cancellationToken);

    public Task<int> CountActiveFinanceApproversAsync(CancellationToken cancellationToken = default)
        => _context.Users
            .Where(u => u.IsActive && u.Role == UserRole.Finance && u.IsFinanceApprover)
            .CountAsync(cancellationToken);
}
