using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Enums;

namespace SureBudgetRequest.Domain.Entities;

public partial class BudgetRequest
{
    /// <summary>
    /// Approve at the current stage. The <paramref name="coaId"/> parameter is
    /// required when the request is at the Finance stage (
    /// <see cref="RequestStatus.PendingFinance"/>) and ignored at all other
    /// stages. Application layer is responsible for verifying that the chosen
    /// Coa exists and is active — the domain only enforces "non-null at the
    /// Finance stage."
    ///
    /// The <paramref name="reconciliationDeadline"/> parameter applies only at
    /// the Finance stage: it is REQUIRED (and must be in the future) when the
    /// request is an <see cref="BudgetRequestType.Advance"/>, and must be null
    /// for every other type. Ignored at non-Finance stages.
    /// </summary>
    public Result ApproveBy(Guid approverId, Guid? coaId = null, DateTime? reconciliationDeadline = null)
    {
        if (Status is not RequestStatus.PendingDeptHead
            and not RequestStatus.PendingManagement
            and not RequestStatus.PendingFinance)
        {
            return Result.Failure($"Cannot approve a request in status '{Status}'.");
        }

        var stage = Status switch
        {
            RequestStatus.PendingDeptHead   => ApprovalStage.DeptHead,
            RequestStatus.PendingManagement => ApprovalStage.Management,
            RequestStatus.PendingFinance    => ApprovalStage.Finance,
            _ => throw new InvalidOperationException("Unreachable")
        };

        // Only DeptHead has a snapshotted assignee. Management and Finance allow ANY
        // user with the matching role — that role check happens in the Application layer.
        if (stage == ApprovalStage.DeptHead && approverId != DeptHeadIdAtSubmission)
            return Result.Failure("This user is not the assigned approver for the current stage.");

        // Defensive check: requester should never be the manual approver at non-Finance/Management stages
        // (auto-approval already handled it at Submit time for DeptHead).
        if (approverId == RequesterId && stage == ApprovalStage.DeptHead)
            return Result.Failure("Requester cannot manually approve their own request.");

        // Finance-stage gate: must pick a Chart of Account.
        if (stage == ApprovalStage.Finance && (!coaId.HasValue || coaId.Value == Guid.Empty))
            return Result.Failure("Chart of Account is required when Finance approves.");

        // Finance-stage gate: advance requests need a future reconciliation
        // deadline; every other type must NOT carry one.
        if (stage == ApprovalStage.Finance)
        {
            if (Type == BudgetRequestType.Advance)
            {
                if (!reconciliationDeadline.HasValue || reconciliationDeadline.Value <= DateTime.UtcNow)
                    return Result.Failure(
                        "Reconciliation deadline is required and must be in the future when Finance approves an advance.");
            }
            else if (reconciliationDeadline.HasValue)
            {
                return Result.Failure("Reconciliation deadline is only valid for advance requests.");
            }
        }

        var now = DateTime.UtcNow;
        _approvalActions.Add(new ApprovalAction(
            Id, stage, ApprovalDecision.Approved, approverId, comment: null, actionedAt: now));

        // Whether the Management stage is required is derivable from the existing
        // submission snapshot — no need for a dedicated flag.
        var requiresManagement = RequestedAmountInMmkAtSubmission > DepartmentLimitAtSubmission;

        // Transition to the next stage
        Status = stage switch
        {
            ApprovalStage.DeptHead   => requiresManagement
                                          ? RequestStatus.PendingManagement
                                          : RequestStatus.PendingFinance,
            ApprovalStage.Management => RequestStatus.PendingFinance,
            ApprovalStage.Finance    => RequestStatus.Approved,
            _ => Status
        };

        // When Finance approves, lock in the approved amount AND assign the COA.
        // Overwriting CoaId is intentional — if Finance approved-then-sent-back-then-
        // re-approved with a different code, the new code wins. The audit trail of
        // approval timestamps in ApprovalActions still tells the story.
        if (stage == ApprovalStage.Finance)
        {
            ApprovedAmount = RequestedAmount;
            CoaId = coaId;

            // Advances carry a reconciliation deadline from this point on. The
            // gate above guarantees it is non-null here for advances and null
            // for every other type.
            if (Type == BudgetRequestType.Advance)
                ReconciliationDeadline = reconciliationDeadline;
        }

        return Result.Success();
    }

    public Result Reject(Guid approverId, string comment)
    {
        if (string.IsNullOrWhiteSpace(comment))
            return Result.Failure("A comment is required when rejecting.");

        var stage = Status switch
        {
            RequestStatus.PendingDeptHead   => (ApprovalStage?)ApprovalStage.DeptHead,
            RequestStatus.PendingManagement => ApprovalStage.Management,
            RequestStatus.PendingFinance    => ApprovalStage.Finance,
            _                               => null
        };

        if (stage is null)
            return Result.Failure($"Cannot reject a request in status '{Status}'.");

        var now = DateTime.UtcNow;
        _approvalActions.Add(new ApprovalAction(
            Id, stage.Value, ApprovalDecision.Rejected, approverId, comment, actionedAt: now));

        Status = RequestStatus.Rejected;
        FinalizedAt = now;
        return Result.Success();
    }

    public Result SendBack(Guid approverId, string comment)
    {
        // Send-back is available at every approval stage. The action is recorded
        // against whichever stage the request currently sits at (mirrors Reject()).
        var stage = Status switch
        {
            RequestStatus.PendingDeptHead   => (ApprovalStage?)ApprovalStage.DeptHead,
            RequestStatus.PendingManagement => ApprovalStage.Management,
            RequestStatus.PendingFinance    => ApprovalStage.Finance,
            _                               => null
        };

        if (stage is null)
            return Result.Failure($"Cannot send back a request in status '{Status}'.");

        if (string.IsNullOrWhiteSpace(comment))
            return Result.Failure("A comment is required when sending back.");

        // Strict: only the assigned dept head may send back at the DeptHead stage
        // (mirrors ApproveBy's assignee check). Management and Finance allow ANY
        // user with the matching role — that role check happens in the Application layer.
        if (stage == ApprovalStage.DeptHead && approverId != DeptHeadIdAtSubmission)
            return Result.Failure("This user is not the assigned approver for the current stage.");

        var now = DateTime.UtcNow;
        _approvalActions.Add(new ApprovalAction(
            Id, stage.Value, ApprovalDecision.SentBack, approverId, comment, actionedAt: now));

        Status = RequestStatus.SentBack;
        // CoaId intentionally preserved — used as a pre-fill hint for the next
        // Finance approval after the requester fixes and resubmits.
        return Result.Success();
    }

    public Result ResubmitAfterSendBack(
        Guid departmentId,
        decimal departmentLimit,
        decimal monthlyLimit,
        decimal monthlySpendBeforeInMmk,
        DateTime monthlyWindowStartUtc,
        DateTime monthlyWindowEndUtc,
        decimal exchangeRateToMmk,
        Guid deptHeadId,
        string deptHeadName,
        string requesterName,
        bool withdrawMethodRequiresAttachment)
    {
        if (Status != RequestStatus.SentBack)
            return Result.Failure("Only sent-back requests can be resubmitted.");

        // R13: restart the chain. Reuses Submit() — the auto-approval logic re-runs
        // because amount/requester/limit/rate may have changed during the fix.
        // The monthly check also re-runs because the dept's cumulative position may
        // have changed since the original submission.
        // sequenceNumber is unused here: Reference was already assigned at the first
        // submission and the `if (Reference is null)` guard inside Submit skips
        // regeneration. Pass 0 so we don't consume a sequence value on resubmit.
        return Submit(
            departmentId, departmentLimit, monthlyLimit, monthlySpendBeforeInMmk,
            monthlyWindowStartUtc, monthlyWindowEndUtc, exchangeRateToMmk,
            deptHeadId, deptHeadName, requesterName, withdrawMethodRequiresAttachment,
            sequenceNumber: 0);
    }
}
