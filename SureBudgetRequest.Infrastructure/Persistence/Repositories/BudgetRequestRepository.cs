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
        DateTime? submittedFromUtc = null,
        DateTime? submittedUntilUtc = null,
        Guid? coaId = null,
        string? currencyCode = null,
        Guid? approverId = null,
        bool? overLimitOnly = null,
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

        // ── Report filters (v4+) ──────────────────────────────────────────────

        // Date range — half-open [from, until). Either bound implies SubmittedAt
        // is non-null (drafts are excluded from date-bounded queries).
        if (submittedFromUtc.HasValue)
            query = query.Where(r => r.SubmittedAt != null && r.SubmittedAt >= submittedFromUtc.Value);

        if (submittedUntilUtc.HasValue)
            query = query.Where(r => r.SubmittedAt != null && r.SubmittedAt < submittedUntilUtc.Value);

        if (coaId.HasValue)
            query = query.Where(r => r.CoaId == coaId.Value);

        if (!string.IsNullOrWhiteSpace(currencyCode))
            query = query.Where(r => r.CurrencyCode == currencyCode);

        if (approverId.HasValue)
        {
            // Match if this user appears anywhere in the approval chain with
            // an approving decision (Approved or AutoApproved — Rejected /
            // SentBack are excluded; those aren't "approvals").
            var aid = approverId.Value;
            query = query.Where(r => r.ApprovalActions.Any(a =>
                a.ApproverId == aid
                && (a.Decision == ApprovalDecision.Approved
                 || a.Decision == ApprovalDecision.AutoApproved)));
        }

        if (overLimitOnly.HasValue)
        {
            query = overLimitOnly.Value
                ? query.Where(r => r.RequestedAmountInMmkAtSubmission > r.DepartmentLimitAtSubmission)
                : query.Where(r => r.RequestedAmountInMmkAtSubmission <= r.DepartmentLimitAtSubmission);
        }

        return await query
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(BudgetRequest budgetRequest, CancellationToken cancellationToken = default)
        => await _context.BudgetRequests.AddAsync(budgetRequest, cancellationToken);

    public async Task<Attachment?> GetAttachmentByIdAsync(Guid attachmentId, CancellationToken cancellationToken = default)
    {
        // AsNoTracking — this is read-only for download, no mutation.
        return await _context.Attachments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == attachmentId, cancellationToken);
    }

    public async Task<decimal> GetMonthlyApprovedSpendInMmkAsync(
        Guid departmentId,
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        // Half-open interval [monthStart, monthEnd) in UTC. Using SubmittedAt
        // because the "month" of a request is defined by submission time, not
        // Finance-approval time. See IBudgetRequestRepository.GetMonthlyApprovedSpendInMmkAsync.
        var monthStart = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1);

        // Statuses that count as "approved by Finance" (i.e. the request has
        // crossed the Finance approval bar).
        var countedStatuses = new[]
        {
            RequestStatus.Approved,
            RequestStatus.PartiallyPaid,
            RequestStatus.Paid
        };

        // SumAsync over an empty set returns 0 (not null) for decimal, so no
        // null-coalesce is needed here.
        return await _context.BudgetRequests
            .AsNoTracking()
            .Where(r => r.DepartmentIdAtSubmission == departmentId
                     && countedStatuses.Contains(r.Status)
                     && r.SubmittedAt != null
                     && r.SubmittedAt >= monthStart
                     && r.SubmittedAt < monthEnd)
            .SumAsync(r => r.RequestedAmountInMmkAtSubmission, cancellationToken);
    }
}
