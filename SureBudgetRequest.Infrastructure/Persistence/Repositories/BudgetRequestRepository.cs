using Microsoft.EntityFrameworkCore;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;

namespace SureBudgetRequest.Infrastructure.Persistence.Repositories;

public sealed class BudgetRequestRepository : IBudgetRequestRepository
{
    private readonly AppDbContext _context;

    public BudgetRequestRepository(AppDbContext context) => _context = context;

    public async Task<BudgetRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Always eager-load child collections — the aggregate must be fully
        // hydrated to enforce domain invariants (e.g. payment sum check).
        return await _context.BudgetRequests
            .Include(r => r.ApprovalActions)
            .Include(r => r.Payments)
            .Include(r => r.Attachments)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<BudgetRequest>> ListAsync(
        Guid? requesterId = null,
        Guid? departmentId = null,
        RequestStatus? status = null,
        IReadOnlyCollection<RequestStatus>? statuses = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.BudgetRequests
            .Include(r => r.ApprovalActions)
            .Include(r => r.Payments)
            .Include(r => r.Attachments)
            .AsQueryable();

        if (requesterId.HasValue)
            query = query.Where(r => r.RequesterId == requesterId.Value);

        if (departmentId.HasValue)
            query = query.Where(r => r.DepartmentIdAtSubmission == departmentId.Value);

        if (status.HasValue)
            query = query.Where(r => r.Status == status.Value);

        if (statuses is { Count: > 0 })
            query = query.Where(r => statuses.Contains(r.Status));

        return await query
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(BudgetRequest budgetRequest, CancellationToken cancellationToken = default)
        => await _context.BudgetRequests.AddAsync(budgetRequest, cancellationToken);
}
