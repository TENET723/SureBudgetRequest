using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Enums;

namespace SureBudgetRequest.Domain.Entities;

public partial class BudgetRequest
{
    // Factory: creates a Draft. Snapshots the dept head at draft time so the
    // request shows its routing target before submission; Submit() will re-snapshot
    // along with department/limit/boss/rate. Limits, boss, and rate are still Submit-time only.
    public static BudgetRequest CreateDraft(
        Guid requesterId,
        string requesterNameAtSubmission,
        BudgetRequestType type,
        Guid deptHeadId,
        string deptHeadName,
        DateTime requestDate,
        decimal requestedAmount,
        string currencyCode,
        string reasons,
        string withdrawerName,
        string withdrawerJobTitle,
        bool allowsPartialPayment,
        string? partialPaymentDetail)
    {
        if (requestedAmount <= 0)
            throw new ArgumentException("Requested amount must be greater than zero.", nameof(requestedAmount));
        if (string.IsNullOrWhiteSpace(currencyCode))
            throw new ArgumentException("Currency code is required.", nameof(currencyCode));
        if (string.IsNullOrWhiteSpace(reasons))
            throw new ArgumentException("Reasons are required.", nameof(reasons));
        if (string.IsNullOrWhiteSpace(withdrawerName))
            throw new ArgumentException("Withdrawer name is required.", nameof(withdrawerName));
        if (string.IsNullOrWhiteSpace(withdrawerJobTitle))
            throw new ArgumentException("Withdrawer job title is required.", nameof(withdrawerJobTitle));
        if (string.IsNullOrWhiteSpace(deptHeadName))
            throw new ArgumentException("Dept head name is required.", nameof(deptHeadName));

        return new BudgetRequest
        {
            //Id = Guid.NewGuid(),
            RequesterId = requesterId,
            RequesterNameAtSubmission = requesterNameAtSubmission,
            DeptHeadIdAtSubmission = deptHeadId,
            DeptHeadNameAtSubmission = deptHeadName,
            Type = type,
            RequestDate = requestDate,
            RequestedAmount = requestedAmount,
            CurrencyCode = currencyCode.Trim().ToUpperInvariant(),
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
        BudgetRequestType type,
        decimal requestedAmount,
        string currencyCode,
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
        if (string.IsNullOrWhiteSpace(currencyCode))
            return Result.Failure("Currency code is required.");
        if (string.IsNullOrWhiteSpace(reasons))
            return Result.Failure("Reasons are required.");
        if (string.IsNullOrWhiteSpace(withdrawerName))
            return Result.Failure("Withdrawer name is required.");
        if (string.IsNullOrWhiteSpace(withdrawerJobTitle))
            return Result.Failure("Withdrawer job title is required.");

        RequestDate = requestDate;
        RequestedAmount = requestedAmount;
        CurrencyCode = currencyCode.Trim().ToUpperInvariant();
        Type = type;
        Reasons = reasons;
        WithdrawerName = withdrawerName;
        WithdrawerJobTitle = withdrawerJobTitle;
        AllowsPartialPayment = allowsPartialPayment;
        PartialPaymentDetail = partialPaymentDetail;
        return Result.Success();
    }

    // Submit moves Draft/SentBack -> PendingDeptHead, fast-forwarding through the
    // DeptHead stage if the requester is the dept head (R9 auto-approval rule).
    // The Management stage (when over limit) does NOT auto-approve — even a Management
    // member's own over-limit request requires peer review by another Management member.
    // This mirrors how Finance works.
    // The MMK-equivalent of the requested amount is compared against the department limit.
    public Result Submit(
        Guid departmentId,
        decimal departmentLimit,             // in MMK
        decimal exchangeRateToMmk,           // current rate for this.CurrencyCode
        Guid deptHeadId,
        string deptHeadName,
        string requesterName)
    {
        if (Status is not RequestStatus.Draft and not RequestStatus.SentBack)
            return Result.Failure($"Cannot submit a request that is in status '{Status}'.");

        if (exchangeRateToMmk <= 0)
            return Result.Failure("Exchange rate must be greater than zero.");
        if (string.IsNullOrWhiteSpace(deptHeadName))
            return Result.Failure("Dept head name is required.");
        if (string.IsNullOrWhiteSpace(requesterName))
            return Result.Failure("Requester name is required.");

        // Convert to MMK for the limit comparison
        var amountInMmk = RequestedAmount * exchangeRateToMmk;
        var isOverLimit = amountInMmk > departmentLimit;

        // Snapshot the routing context (R7, R12)
        DepartmentIdAtSubmission = departmentId;
        DepartmentLimitAtSubmission = departmentLimit;
        ExchangeRateAtSubmission = exchangeRateToMmk;
        RequestedAmountInMmkAtSubmission = amountInMmk;
        DeptHeadIdAtSubmission = deptHeadId;
        DeptHeadNameAtSubmission = deptHeadName;
        RequesterNameAtSubmission = requesterName;
        SubmittedAt = DateTime.UtcNow;

        // Fast-forward through DeptHead if requester == dept head (R9)
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

        // Stage 2: Management (only if over limit) — never auto-approves.
        // Any Management member can approve; identity is checked by role in Application.
        if (isOverLimit)
        {
            Status = RequestStatus.PendingManagement;
            return Result.Success();
        }

        // Stage 3: Finance — never auto-approves either.
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
