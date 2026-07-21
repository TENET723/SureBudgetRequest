using Microsoft.EntityFrameworkCore;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.BudgetRequests.Queries.SearchBudgetRequests;
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
        // hydrated to enforce domain invariants (e.g. payment sum check, and the
        // advance-usage no-overspending check).
        return await _context.BudgetRequests
            .Include(r => r.ApprovalActions)
            .Include(r => r.Payments)
                .ThenInclude(p => p.Receipts)
            .Include(r => r.Attachments)
            .Include(r => r.AdvanceUsages)
                .ThenInclude(u => u.Receipts)
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
        IReadOnlyCollection<BudgetRequestType>? types = null,
        decimal? amountInMmkFrom = null,
        decimal? amountInMmkTo = null,
        bool? periodOverrunOnly = null,
        string? searchTerm = null,
        CancellationToken cancellationToken = default)
    {
        var query = BuildFilteredQuery(
            requesterId, departmentId, status, statuses, submittedFromUtc, submittedUntilUtc,
            coaId, currencyCode, approverId, overLimitOnly, types, amountInMmkFrom, amountInMmkTo,
            periodOverrunOnly, searchTerm);

        return await query
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Builds the eager-loaded, filtered query shared by <see cref="ListAsync"/>
    /// and <see cref="SearchAsync"/>. Applies every filter dimension plus the
    /// optional free-text search, but does NOT apply sorting or paging — callers
    /// add those.
    /// </summary>
    private IQueryable<BudgetRequest> BuildFilteredQuery(
        Guid? requesterId,
        Guid? departmentId,
        RequestStatus? status,
        IReadOnlyCollection<RequestStatus>? statuses,
        DateTime? submittedFromUtc,
        DateTime? submittedUntilUtc,
        Guid? coaId,
        string? currencyCode,
        Guid? approverId,
        bool? overLimitOnly,
        IReadOnlyCollection<BudgetRequestType>? types,
        decimal? amountInMmkFrom,
        decimal? amountInMmkTo,
        bool? periodOverrunOnly,
        string? searchTerm)
    {
        var query = _context.BudgetRequests
            .Include(r => r.ApprovalActions)
            .Include(r => r.Payments)
                .ThenInclude(p => p.Receipts)
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

        // Type — IN semantics, same shape as the statuses filter above.
        if (types is { Count: > 0 })
            query = query.Where(r => types.Contains(r.Type));

        // Amount range — inclusive bounds on the MMK-at-submission amount.
        if (amountInMmkFrom.HasValue)
            query = query.Where(r => r.RequestedAmountInMmkAtSubmission >= amountInMmkFrom.Value);

        if (amountInMmkTo.HasValue)
            query = query.Where(r => r.RequestedAmountInMmkAtSubmission <= amountInMmkTo.Value);

        // Monthly overrun — mirrors the MonthlyLimitBadge.IsOver condition exactly,
        // computed from the submission snapshots. true = crossed the monthly cap; false =
        // the inverse; null = no filter.
        if (periodOverrunOnly.HasValue)
        {
            query = periodOverrunOnly.Value
                ? query.Where(r =>
                    r.MonthlyLimitAtSubmission != null
                    && r.MonthlySpendBeforeAtSubmission != null
                    && (r.MonthlySpendBeforeAtSubmission + r.RequestedAmountInMmkAtSubmission) > r.MonthlyLimitAtSubmission)
                : query.Where(r => !(
                    r.MonthlyLimitAtSubmission != null
                    && r.MonthlySpendBeforeAtSubmission != null
                    && (r.MonthlySpendBeforeAtSubmission + r.RequestedAmountInMmkAtSubmission) > r.MonthlyLimitAtSubmission));
        }

        // Free-text search — case-insensitive substring (ILike) against Reasons,
        // RequesterNameAtSubmission and Reference (Reference is nullable, so guard
        // it so the OR survives a null without NRE in translation).
        var term = searchTerm?.Trim();
        if (!string.IsNullOrEmpty(term))
        {
            var pattern = $"%{term}%";
            query = query.Where(r =>
                EF.Functions.ILike(r.Reasons, pattern)
                || EF.Functions.ILike(r.RequesterNameAtSubmission, pattern)
                || (r.Reference != null && EF.Functions.ILike(r.Reference, pattern)));
        }

        return query;
    }

    public async Task<(IReadOnlyList<BudgetRequest> Items, int TotalCount)> SearchAsync(
        Guid? requesterId,
        Guid? departmentId,
        RequestStatus? status,
        IReadOnlyCollection<RequestStatus>? statuses,
        DateTime? submittedFromUtc,
        DateTime? submittedUntilUtc,
        Guid? coaId,
        string? currencyCode,
        Guid? approverId,
        bool? overLimitOnly,
        IReadOnlyCollection<BudgetRequestType>? types,
        decimal? amountInMmkFrom,
        decimal? amountInMmkTo,
        bool? periodOverrunOnly,
        string? searchTerm,
        bool? overdueAdvanceOnly,
        BudgetRequestSortBy sortBy,
        bool sortDescending,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = BuildFilteredQuery(
            requesterId, departmentId, status, statuses, submittedFromUtc, submittedUntilUtc,
            coaId, currencyCode, approverId, overLimitOnly, types, amountInMmkFrom, amountInMmkTo,
            periodOverrunOnly, searchTerm);

        // Overdue-advance filter — the DB-side mirror of
        // BudgetRequestSummaryDto.IsOverdueAdvance, evaluated against a single
        // UTC "now" captured here so the COUNT and the page see the same cutoff.
        if (overdueAdvanceOnly.HasValue)
        {
            var nowUtc = DateTime.UtcNow;
            query = overdueAdvanceOnly.Value
                ? query.Where(r =>
                    r.Type == BudgetRequestType.Advance
                    && r.Status == RequestStatus.PendingReconciliation
                    && r.ReconciliationDeadline != null
                    && r.ReconciliationDeadline < nowUtc)
                : query.Where(r => !(
                    r.Type == BudgetRequestType.Advance
                    && r.Status == RequestStatus.PendingReconciliation
                    && r.ReconciliationDeadline != null
                    && r.ReconciliationDeadline < nowUtc));
        }

        // Total before paging.
        var totalCount = await query.CountAsync(ct);

        // Sort by the requested column, then a stable tiebreaker on CreatedAt.
        query = ApplySort(query, sortBy, sortDescending);

        // Clamp paging into sane bounds.
        var safePage = page < 1 ? 1 : page;
        var safePageSize = pageSize < 1 ? 1 : (pageSize > 100 ? 100 : pageSize);

        var items = await query
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    private static IQueryable<BudgetRequest> ApplySort(
        IQueryable<BudgetRequest> query, BudgetRequestSortBy sortBy, bool descending)
    {
        IOrderedQueryable<BudgetRequest> ordered = sortBy switch
        {
            BudgetRequestSortBy.RequestDate => descending
                ? query.OrderByDescending(r => r.RequestDate)
                : query.OrderBy(r => r.RequestDate),
            BudgetRequestSortBy.Reference => descending
                ? query.OrderByDescending(r => r.Reference)
                : query.OrderBy(r => r.Reference),
            BudgetRequestSortBy.Requester => descending
                ? query.OrderByDescending(r => r.RequesterNameAtSubmission)
                : query.OrderBy(r => r.RequesterNameAtSubmission),
            BudgetRequestSortBy.Type => descending
                ? query.OrderByDescending(r => r.Type)
                : query.OrderBy(r => r.Type),
            BudgetRequestSortBy.AmountInMmk => descending
                ? query.OrderByDescending(r => r.RequestedAmountInMmkAtSubmission)
                : query.OrderBy(r => r.RequestedAmountInMmkAtSubmission),
            BudgetRequestSortBy.Status => descending
                ? query.OrderByDescending(r => r.Status)
                : query.OrderBy(r => r.Status),
            // "Waiting since" for the Outstanding Payments queue — translates to
            // COALESCE(finalized_at, submitted_at, created_at). Ascending puts
            // the longest-waiting request first.
            BudgetRequestSortBy.OutstandingSince => descending
                ? query.OrderByDescending(r => r.FinalizedAt ?? r.SubmittedAt ?? r.CreatedAt)
                : query.OrderBy(r => r.FinalizedAt ?? r.SubmittedAt ?? r.CreatedAt),
            // Advance reconciliation deadline — Inbox overdue view. Ascending
            // puts the most-overdue advance first.
            BudgetRequestSortBy.ReconciliationDeadline => descending
                ? query.OrderByDescending(r => r.ReconciliationDeadline)
                : query.OrderBy(r => r.ReconciliationDeadline),
            // SubmittedAt is the default.
            _ => descending
                ? query.OrderByDescending(r => r.SubmittedAt)
                : query.OrderBy(r => r.SubmittedAt),
        };

        return ordered.ThenByDescending(r => r.CreatedAt);
    }

    public async Task AddAsync(BudgetRequest budgetRequest, CancellationToken cancellationToken = default)
        => await _context.BudgetRequests.AddAsync(budgetRequest, cancellationToken);

    public async Task<long> NextReferenceSequenceAsync(CancellationToken cancellationToken = default)
    {
        // nextval() is atomic and race-condition safe — no lock or counter table.
        // SqlQueryRaw<long> requires the scalar column be aliased "Value".
        var rows = await _context.Database
            .SqlQueryRaw<long>("SELECT nextval('budget_request_ref_seq') AS \"Value\"")
            .ToListAsync(cancellationToken);
        return rows.Single();
    }

    public async Task<Attachment?> GetAttachmentByIdAsync(Guid attachmentId, CancellationToken cancellationToken = default)
    {
        // AsNoTracking — this is read-only for download, no mutation.
        return await _context.Attachments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == attachmentId, cancellationToken);
    }

    public async Task<decimal> GetApprovedSpendInMmkAsync(
        Guid departmentId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        // Half-open interval [fromUtc, toUtc) on SubmittedAt — the "period" of a
        // request is defined by submission time, not Finance-approval time.
        // See IBudgetRequestRepository.GetApprovedSpendInMmkAsync.

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
                     && r.SubmittedAt >= fromUtc
                     && r.SubmittedAt < toUtc)
            .SumAsync(r => r.RequestedAmountInMmkAtSubmission, cancellationToken);
    }

    public async Task<bool> HasOverdueAdvanceAsync(
        Guid requesterId,
        DateTime asOfUtc,
        CancellationToken cancellationToken = default)
    {
        var result = await _context.BudgetRequests
            .AsNoTracking()
            .AnyAsync(r => r.RequesterId == requesterId
                        && r.Type == BudgetRequestType.Advance
                        && r.ReconciliationDeadline != null
                        && r.ReconciliationDeadline < asOfUtc
                        && (r.Status == RequestStatus.PendingReconciliation
                         || r.Status == RequestStatus.AwaitingRefund),
                cancellationToken);

        //var test = await _context.BudgetRequests
        //    .AsNoTracking()
        //    .FirstAsync(r => r.RequesterId == requesterId
        //                && r.Type == BudgetRequestType.Advance
        //                && r.ReconciliationDeadline != null
        //                && r.ReconciliationDeadline < asOfUtc
        //                && (r.Status == RequestStatus.PendingReconciliation
        //                 || r.Status == RequestStatus.AwaitingRefund),
        //        cancellationToken);
        return result;
    }
}
