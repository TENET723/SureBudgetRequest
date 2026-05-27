using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Enums;

namespace SureBudgetRequest.Domain.Entities;

public partial class BudgetRequest
{
    // Hard cap on attachments per request. Enforced as an aggregate invariant so
    // it can't be bypassed by skipping the Application validator.
    private const int MaxAttachmentsPerRequest = 10;

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
        Guid withdrawMethodId,
        bool allowsPartialPayment,
        string? partialPaymentDetail,
        string? monthlyOverrunJustification = null,
        decimal? manualExchangeRate = null)
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
        if (withdrawMethodId == Guid.Empty)
            throw new ArgumentException("Withdraw method is required.", nameof(withdrawMethodId));
        if (string.IsNullOrWhiteSpace(deptHeadName))
            throw new ArgumentException("Dept head name is required.", nameof(deptHeadName));

        if (type == BudgetRequestType.Advance && manualExchangeRate.HasValue)
            throw new ArgumentException("Manual exchange rate is not allowed for Advance requests.", nameof(manualExchangeRate));

        if (manualExchangeRate.HasValue && manualExchangeRate <= 0)
            throw new ArgumentException("Manual exchange rate must be greater than zero.", nameof(manualExchangeRate));

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
            ManualExchangeRate = manualExchangeRate,
            Reasons = reasons,
            WithdrawerName = withdrawerName,
            WithdrawerJobTitle = withdrawerJobTitle,
            WithdrawMethodId = withdrawMethodId,
            AllowsPartialPayment = allowsPartialPayment,
            PartialPaymentDetail = partialPaymentDetail,
            MonthlyOverrunJustification = NormalizeJustification(monthlyOverrunJustification),
            Status = RequestStatus.Draft,
            ApprovedAmount = 0,
            CreatedAt = DateTime.UtcNow
        };
    }

    // Allow editing fields while in Draft, SentBack, or PendingDeptHead (for DH modification).
    public Result UpdateDetails(
        DateTime requestDate,
        BudgetRequestType type,
        decimal requestedAmount,
        string currencyCode,
        string reasons,
        string withdrawerName,
        string withdrawerJobTitle,
        Guid withdrawMethodId,
        bool allowsPartialPayment,
        string? partialPaymentDetail,
        string? monthlyOverrunJustification = null,
        decimal? manualExchangeRate = null)
    {
        if (Status is not RequestStatus.Draft and not RequestStatus.SentBack and not RequestStatus.PendingDeptHead)
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
        if (withdrawMethodId == Guid.Empty)
            return Result.Failure("Withdraw method is required.");

        if (type == BudgetRequestType.Advance && manualExchangeRate.HasValue)
            return Result.Failure("Manual exchange rate is not allowed for Advance requests.");

        if (manualExchangeRate.HasValue && manualExchangeRate <= 0)
            return Result.Failure("Manual exchange rate must be greater than zero.");

        RequestDate = requestDate;
        RequestedAmount = requestedAmount;
        CurrencyCode = currencyCode.Trim().ToUpperInvariant();
        Type = type;
        ManualExchangeRate = manualExchangeRate;
        Reasons = reasons;
        WithdrawerName = withdrawerName;
        WithdrawerJobTitle = withdrawerJobTitle;
        WithdrawMethodId = withdrawMethodId;
        AllowsPartialPayment = allowsPartialPayment;
        PartialPaymentDetail = partialPaymentDetail;
        MonthlyOverrunJustification = NormalizeJustification(monthlyOverrunJustification);

        // If the request was already submitted (PendingDeptHead or SentBack), we must
        // update the MMK snapshot to reflect the new amount, using the exchange rate
        // that was locked in at submission time.
        if (SubmittedAt.HasValue)
        {
            RequestedAmountInMmkAtSubmission = RequestedAmount * ExchangeRateAtSubmission;
        }

        return Result.Success();
    }

    // Submit moves Draft/SentBack -> PendingDeptHead, fast-forwarding through the
    // DeptHead stage if the requester is the dept head (R9 auto-approval rule).
    // The Management stage (when over per-request limit) does NOT auto-approve — even a
    // Management member's own over-limit request requires peer review by another Management
    // member. This mirrors how Finance works.
    //
    // Monthly limit (R15/R16): if the department has a monthly limit configured
    // (non-null) AND (monthlySpendBeforeInMmk + amountInMmk) > monthlyLimit, the
    // requester must have supplied a non-empty MonthlyOverrunJustification. The
    // monthly check does NOT alter routing — only the per-request limit triggers
    // the Management stage. Monthly enforcement is independent.
    //
    // The MMK-equivalent of the requested amount is compared against both limits.
    public Result Submit(
        Guid departmentId,
        decimal departmentLimit,             // in MMK
        decimal? monthlyLimit,               // in MMK; null = monthly enforcement disabled for this dept
        decimal? monthlySpendBeforeInMmk,    // in MMK; pass null when monthlyLimit is null
        decimal exchangeRateToMmk,           // current system rate for this.CurrencyCode
        Guid deptHeadId,
        string deptHeadName,
        string requesterName,
        bool withdrawMethodRequiresAttachment)
    {
        if (Status is not RequestStatus.Draft and not RequestStatus.SentBack)
            return Result.Failure($"Cannot submit a request that is in status '{Status}'.");

        // Priority: Manual rate override (locked at draft time) > passed-in system rate.
        var effectiveRate = ManualExchangeRate ?? exchangeRateToMmk;

        if (effectiveRate <= 0)
            return Result.Failure("Exchange rate must be greater than zero.");
        if (string.IsNullOrWhiteSpace(deptHeadName))
            return Result.Failure("Dept head name is required.");
        if (string.IsNullOrWhiteSpace(requesterName))
            return Result.Failure("Requester name is required.");
        if (!WithdrawMethodId.HasValue)
            return Result.Failure("Withdraw method is required.");

        // Banking-info attachment guard. The chosen WithdrawMethod's
        // RequiresAttachment flag is passed in by the Application layer (which
        // looked it up) — domain doesn't reach into the master record.
        if (withdrawMethodRequiresAttachment
            && !_attachments.Any(a => a.Category == AttachmentCategory.WithdrawMethod))
        {
            return Result.Failure(
                "This withdraw method requires a banking-info attachment. " +
                "Please upload at least one banking file before submitting.");
        }

        // Consistency check: spend must be supplied iff the dept has a monthly limit.
        if (monthlyLimit.HasValue != monthlySpendBeforeInMmk.HasValue)
            return Result.Failure(
                "Monthly limit and monthly spend snapshot must both be supplied, or both be null.");

        // Convert to MMK for the limit comparisons
        var amountInMmk = RequestedAmount * effectiveRate;
        var isOverPerRequestLimit = amountInMmk > departmentLimit;

        // Monthly check — only when the department has a monthly limit configured.
        var isOverMonthly = monthlyLimit.HasValue
                         && (monthlySpendBeforeInMmk!.Value + amountInMmk) > monthlyLimit.Value;

        if (isOverMonthly && string.IsNullOrWhiteSpace(MonthlyOverrunJustification))
            return Result.Failure(
                "This request would push the department over its monthly limit. " +
                "A justification is required.");

        // Snapshot the routing context (R7, R12, R15, R16)
        DepartmentIdAtSubmission = departmentId;
        DepartmentLimitAtSubmission = departmentLimit;
        MonthlyLimitAtSubmission = monthlyLimit;
        MonthlySpendBeforeAtSubmission = monthlySpendBeforeInMmk;
        ExchangeRateAtSubmission = effectiveRate;
        RequestedAmountInMmkAtSubmission = amountInMmk;
        DeptHeadIdAtSubmission = deptHeadId;
        DeptHeadNameAtSubmission = deptHeadName;
        RequesterNameAtSubmission = requesterName;
        SubmittedAt = DateTime.UtcNow;

        // Clear the justification snapshot if it ended up not being needed.
        // We intentionally do NOT clear it — keeping it preserves the requester's
        // pre-submission expectation in the audit trail. Comment kept for future
        // reviewers who might wonder. (If you change your mind, set it to null here
        // when !isOverMonthly.)

        // Generate the human-friendly reference number.
        // Only set on first submission — resubmission after SendBack keeps the same Reference.
        if (Reference is null)
        {
            Reference = GenerateReference(Type, SubmittedAt.Value);
        }

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

        // Stage 2: Management (only if over per-request limit) — never auto-approves.
        // Any Management member can approve; identity is checked by role in Application.
        // Note: monthly overrun does NOT route here — only the per-request limit does.
        if (isOverPerRequestLimit)
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

    /// <summary>
    /// Adds an attachment to the request. Normally only allowed while the request
    /// is editable (Draft or SentBack) and only by the requester. The one
    /// exception is a <see cref="AttachmentCategory.UsageReceipt"/> file, which
    /// may also be added while the request is in
    /// <see cref="RequestStatus.PendingReconciliation"/> — that is how the
    /// requester attaches receipts to advance-usage line items.
    /// </summary>
    public Result AddAttachment(
        string fileName,
        string storedPath,
        string contentType,
        long sizeBytes,
        Guid uploadedByUserId,
        AttachmentCategory category = AttachmentCategory.General)
    {
        var isReconciliationReceipt =
              category == AttachmentCategory.UsageReceipt
              && Status == RequestStatus.PendingReconciliation;

        // NEW: Bypass lane for Finance uploading payment receipts
        var isPaymentReceipt =
            category == AttachmentCategory.PaymentReceipt
            && Status is RequestStatus.Approved or RequestStatus.PartiallyPaid;

        if (Status is not RequestStatus.Draft and not RequestStatus.SentBack
            && !isReconciliationReceipt
            && !isPaymentReceipt)
        {
            return Result.Failure($"Cannot add attachment for category '{category}' while status is '{Status}'.");
        }

        // The requester owns Draft, SentBack, and Reconciliation files.
        // Finance owns Payment files. We just ensure non-Finance uploads are restricted to the Requester.
        if (!isPaymentReceipt && uploadedByUserId != RequesterId)
            return Result.Failure("Only the requester can add this type of attachment.");

        if (string.IsNullOrWhiteSpace(fileName))
            return Result.Failure("File name is required.");
        if (string.IsNullOrWhiteSpace(storedPath))
            return Result.Failure("Stored path is required.");
        if (sizeBytes <= 0)
            return Result.Failure("File size must be greater than zero.");

        if (_attachments.Count >= MaxAttachmentsPerRequest)
            return Result.Failure($"Cannot add more than {MaxAttachmentsPerRequest} attachments to a request.");

        _attachments.Add(new Attachment(Id, fileName, storedPath, contentType, sizeBytes, uploadedByUserId, category));
        return Result.Success();
    }

    /// <summary>
    /// Removes an attachment. Only the requester can remove, and only while the
    /// request is in Draft or SentBack. Returns the <c>StoredPath</c> of the removed
    /// attachment so the caller can delete it from physical storage.
    /// </summary>
    public Result<string> RemoveAttachment(Guid attachmentId, Guid byUserId)
    {
        if (Status is not RequestStatus.Draft and not RequestStatus.SentBack)
            return Result<string>.Failure($"Attachments can only be removed while the request is in Draft or SentBack (current: '{Status}').");

        if (byUserId != RequesterId)
            return Result<string>.Failure("Only the requester can remove attachments from their request.");

        var attachment = _attachments.FirstOrDefault(a => a.Id == attachmentId);
        if (attachment is null)
            return Result<string>.Failure("Attachment not found on this request.");

        var storedPath = attachment.StoredPath;
        _attachments.Remove(attachment);
        return Result<string>.Success(storedPath);
    }

    // === Reference generation ===
    // Format: BR-{TypeCode}-{yyyyMMdd}-{random4}  e.g. "BR-U-20260513-4521"
    // 4-digit random keeps collision rate negligible at internal-app volume.
    // A DB unique index + retry at the command-handler level should be added for safety.
    private static string GenerateReference(BudgetRequestType type, DateTime submittedAtUtc)
    {
        var typeCode = type switch
        {
            BudgetRequestType.Urgent           => "U",
            BudgetRequestType.Standard         => "S",
            BudgetRequestType.ProjectProposal  => "P",
            BudgetRequestType.Advance          => "A",
            _ => "X"
        };
        var datePart = submittedAtUtc.ToString("yyyyMMdd");
        var rand = Random.Shared.Next(1000, 10000); // 1000-9999 inclusive
        return $"BR-{typeCode}-{datePart}-{rand}";
    }

    // Normalize whitespace-only strings to null so the "is justification present?"
    // check in Submit() doesn't have to repeat IsNullOrWhiteSpace.
    private static string? NormalizeJustification(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
