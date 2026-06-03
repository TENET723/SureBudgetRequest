using SureBudgetRequest.Application.BudgetRequests.Queries.SearchBudgetRequests;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;

namespace SureBudgetRequest.Application.Abstractions.Repositories;

public interface IBudgetRequestRepository
{
    Task<BudgetRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns requests matching the supplied filters. Any/all parameters are
    /// optional; null means "no filter on this dimension". Role-based scoping
    /// is the caller's responsibility (Inbox.razor / OutstandingPayments.razor
    /// / the report page each apply their own scoping rules).
    /// </summary>
    /// <remarks>
    /// New filter parameters added for the report page (v4):
    /// <list type="bullet">
    ///   <item><c>submittedFromUtc</c> / <c>submittedUntilUtc</c> — half-open
    ///   range on <c>SubmittedAt</c>. Drafts (null SubmittedAt) are excluded
    ///   when either bound is supplied.</item>
    ///   <item><c>coaId</c> — match the assigned Chart of Account.</item>
    ///   <item><c>currencyCode</c> — match the request currency.</item>
    ///   <item><c>approverId</c> — match requests where this user appears in
    ///   the approval chain with Approved/AutoApproved decision at any
    ///   stage.</item>
    ///   <item><c>overLimitOnly</c> — null = either; true = only over-limit
    ///   (routed through Management); false = only within-limit.</item>
    ///   <item><c>types</c> — null/empty = any type; otherwise match requests
    ///   whose <c>Type</c> is in the supplied set (IN semantics).</item>
    ///   <item><c>amountInMmkFrom</c> / <c>amountInMmkTo</c> — inclusive bounds
    ///   on <c>RequestedAmountInMmkAtSubmission</c>; null = unbounded on that
    ///   side.</item>
    ///   <item><c>periodOverrunOnly</c> — null = either; true = only requests
    ///   that crossed their department's cumulative (period) cap at submission;
    ///   false = only those that did not.</item>
    /// </list>
    /// </remarks>
    Task<IReadOnlyList<BudgetRequest>> ListAsync(
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
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Paged, sorted, server-side search. Applies the same filter dimensions as
    /// <see cref="ListAsync"/> (shared filter logic), then an optional free-text
    /// <paramref name="searchTerm"/> (case-insensitive substring match against
    /// Reasons, RequesterNameAtSubmission and Reference), sorts by
    /// <paramref name="sortBy"/>/<paramref name="sortDescending"/>, and returns a
    /// single page along with the total matching count (computed before paging).
    /// <paramref name="page"/> is clamped to >= 1 and <paramref name="pageSize"/>
    /// to [1, 100].
    /// </summary>
    Task<(IReadOnlyList<BudgetRequest> Items, int TotalCount)> SearchAsync(
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
        BudgetRequestSortBy sortBy,
        bool sortDescending,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task AddAsync(BudgetRequest budgetRequest, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches a single attachment by its own Id, without loading the parent aggregate.
    /// Used by the download endpoint, which only knows the attachment Id.
    /// Returns null if no such attachment exists.
    /// </summary>
    Task<Attachment?> GetAttachmentByIdAsync(Guid attachmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the sum of <c>RequestedAmountInMmkAtSubmission</c> for the given
    /// department's requests whose <c>SubmittedAt</c> falls in the half-open UTC
    /// interval <c>[fromUtc, toUtc)</c> and whose <c>Status</c> is Finance-approved
    /// (<c>Approved</c>, <c>PartiallyPaid</c>, or <c>Paid</c>).
    ///
    /// Pending*, Draft, SentBack, Rejected, and Cancelled requests are NOT
    /// counted.
    ///
    /// The "period" of a request is defined by its <c>SubmittedAt</c> timestamp,
    /// not by when Finance approved it — i.e. a request submitted on the last day
    /// of a period counts toward that period even if Finance approves it later.
    /// </summary>
    Task<decimal> GetApprovedSpendInMmkAsync(
        Guid departmentId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns true if the requester has any Advance whose
    /// <c>ReconciliationDeadline</c> is before <paramref name="asOfUtc"/> and
    /// whose <c>Status</c> is <c>PendingReconciliation</c> or <c>AwaitingRefund</c>.
    /// </summary>
    Task<bool> HasOverdueAdvanceAsync(
        Guid requesterId,
        DateTime asOfUtc,
        CancellationToken cancellationToken = default);
}
