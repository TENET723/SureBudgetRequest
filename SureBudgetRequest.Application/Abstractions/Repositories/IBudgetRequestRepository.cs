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
        CancellationToken cancellationToken = default);

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
}
