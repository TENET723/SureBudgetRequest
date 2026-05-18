using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;

namespace SureBudgetRequest.Application.Abstractions.Repositories;

public interface IBudgetRequestRepository
{
    Task<BudgetRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns requests visible to the given user based on their role:
    /// - Employee: only their own requests.
    /// - DeptHead: requests from their department.
    /// - Management: all over-limit requests at PendingManagement.
    /// - Finance: all requests at PendingFinance or beyond.
    /// - Admin: all requests.
    /// </summary>
    Task<IReadOnlyList<BudgetRequest>> ListAsync(
        Guid? requesterId = null,
        Guid? departmentId = null,
        RequestStatus? status = null,
        IReadOnlyCollection<RequestStatus>? statuses = null,
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
    /// department's requests whose <c>SubmittedAt</c> falls in the given UTC
    /// calendar month (<paramref name="year"/>, <paramref name="month"/>) and
    /// whose <c>Status</c> is Finance-approved (<c>Approved</c>,
    /// <c>PartiallyPaid</c>, or <c>Paid</c>).
    ///
    /// Pending*, Draft, SentBack, Rejected, and Cancelled requests are NOT
    /// counted.
    ///
    /// The "month" of a request is defined by its <c>SubmittedAt</c> timestamp,
    /// not by when Finance approved it — i.e. a request submitted Jan 30 counts
    /// toward January even if Finance approves it Feb 2.
    /// </summary>
    Task<decimal> GetMonthlyApprovedSpendInMmkAsync(
        Guid departmentId,
        int year,
        int month,
        CancellationToken cancellationToken = default);
}
