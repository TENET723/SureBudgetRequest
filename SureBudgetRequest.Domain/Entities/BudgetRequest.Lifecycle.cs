using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Enums;

namespace SureBudgetRequest.Domain.Entities;

public partial class BudgetRequest
{
    // Factory: creates a Draft. Doesn't take limits or dept head — those are
    // snapshotted at Submit() time, not Draft time.
    public static BudgetRequest CreateDraft(
        Guid requesterId,
        DateTime requestDate,
        decimal requestedAmount,
        string reasons,
        string withdrawerName,
        string withdrawerJobTitle,
        bool allowsPartialPayment,
        string? partialPaymentDetail)
    {
        if (requestedAmount <= 0)
            throw new ArgumentException("Requested amount must be greater than zero.", nameof(requestedAmount));
        if (string.IsNullOrWhiteSpace(reasons))
            throw new ArgumentException("Reasons are required.", nameof(reasons));
        if (string.IsNullOrWhiteSpace(withdrawerName))
            throw new ArgumentException("Withdrawer name is required.", nameof(withdrawerName));
        if (string.IsNullOrWhiteSpace(withdrawerJobTitle))
            throw new ArgumentException("Withdrawer job title is required.", nameof(withdrawerJobTitle));

        return new BudgetRequest
        {
            Id = Guid.NewGuid(),
            RequesterId = requesterId,
            RequestDate = requestDate,
            RequestedAmount = requestedAmount,
            Reasons = reasons,
            WithdrawerName = withdrawerName,
            WithdrawerJobTitle = withdrawerJobTitle,
            AllowsPartialPayment = allowsPartialPayment,
            PartialPaymentDetail = partialPaymentDetail,
            Status = RequestStatus.Draft,
            ApprovedAmount = 0,
            CreatedAt = DateTime.UtcNow
        };
    }

    // Allow editing fields while in Draft or SentBack.
    public Result UpdateDetails(
        DateTime requestDate,
        decimal requestedAmount,
        string reasons,
        string withdrawerName,
        string withdrawerJobTitle,
        bool allowsPartialPayment,
        string? partialPaymentDetail)
    {
        if (Status is not RequestStatus.Draft and not RequestStatus.SentBack)
            return Result.Failure($"Cannot edit a request in status '{Status}'.");

        if (requestedAmount <= 0)
            return Result.Failure("Requested amount must be greater than zero.");
        if (string.IsNullOrWhiteSpace(reasons))
            return Result.Failure("Reasons are required.");
        if (string.IsNullOrWhiteSpace(withdrawerName))
            return Result.Failure("Withdrawer name is required.");
        if (string.IsNullOrWhiteSpace(withdrawerJobTitle))
            return Result.Failure("Withdrawer job title is required.");

        RequestDate = requestDate;
        RequestedAmount = requestedAmount;
        Reasons = reasons;
        WithdrawerName = withdrawerName;
        WithdrawerJobTitle = withdrawerJobTitle;
        AllowsPartialPayment = allowsPartialPayment;
        PartialPaymentDetail = partialPaymentDetail;
        return Result.Success();
    }

    // Submit moves Draft/SentBack -> PendingDeptHead, fast-forwarding through any
    // stages where the requester is the assigned approver (R9 auto-approval rule).
    public Result Submit(
        Guid departmentId,
        decimal departmentLimit,
        Guid deptHeadId,
        Guid? bossId)   // null if amount <= limit
    {
        if (Status is not RequestStatus.Draft and not RequestStatus.SentBack)
            return Result.Failure($"Cannot submit a request that is in status '{Status}'.");

        // Validate boss presence matches over-limit logic
        var isOverLimit = RequestedAmount > departmentLimit;
        if (isOverLimit && bossId is null)
            return Result.Failure("Boss must be provided for over-limit requests.");
        if (!isOverLimit && bossId is not null)
            return Result.Failure("Boss must not be provided for under-limit requests.");

        // Snapshot the routing context
        DepartmentIdAtSubmission = departmentId;
        DepartmentLimitAtSubmission = departmentLimit;
        DeptHeadIdAtSubmission = deptHeadId;
        BossIdAtSubmission = bossId;
        SubmittedAt = DateTime.UtcNow;

        // Fast-forward through stages where requester == approver (R9)
        var now = DateTime.UtcNow;

        // Stage 1: DeptHead
        if (deptHeadId == RequesterId)
        {
            _approvalActions.Add(new ApprovalAction(
                Id, ApprovalStage.DeptHead, ApprovalDecision.AutoApproved,
                RequesterId, comment: null, actionedAt: now));
        }
        else
        {
            Status = RequestStatus.PendingDeptHead;
            return Result.Success();
        }

        // Stage 2: Boss (only if over limit)
        if (bossId is not null)
        {
            if (bossId == RequesterId)
            {
                _approvalActions.Add(new ApprovalAction(
                    Id, ApprovalStage.Boss, ApprovalDecision.AutoApproved,
                    RequesterId, comment: null, actionedAt: now));
            }
            else
            {
                Status = RequestStatus.PendingBoss;
                return Result.Success();
            }
        }

        // Stage 3: Finance — Finance never auto-approves.
        // Even if a Finance member submits, another Finance member must approve.
        Status = RequestStatus.PendingFinance;
        return Result.Success();
    }

    public Result Cancel(Guid byUserId)
    {
        if (byUserId != RequesterId)
            return Result.Failure("Only the requester can cancel their request.");

        if (Status is not RequestStatus.PendingDeptHead and not RequestStatus.SentBack and not RequestStatus.Draft)
            return Result.Failure($"Cannot cancel a request in status '{Status}'.");

        Status = RequestStatus.Cancelled;
        FinalizedAt = DateTime.UtcNow;
        return Result.Success();
    }

    public Result AddAttachment(
        string fileName,
        string storedPath,
        string contentType,
        long sizeBytes,
        Guid uploadedByUserId)
    {
        if (IsTerminal)
            return Result.Failure($"Cannot add attachments to a request in status '{Status}'.");

        if (string.IsNullOrWhiteSpace(fileName))
            return Result.Failure("File name is required.");
        if (sizeBytes <= 0)
            return Result.Failure("File size must be greater than zero.");

        _attachments.Add(new Attachment(Id, fileName, storedPath, contentType, sizeBytes, uploadedByUserId));
        return Result.Success();
    }
}
