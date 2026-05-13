using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Enums;

namespace SureBudgetRequest.Domain.Entities;

public partial class BudgetRequest
{
    public Result ApproveBy(Guid approverId)
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

        // When Finance approves, lock in the approved amount.
        if (stage == ApprovalStage.Finance)
            ApprovedAmount = RequestedAmount;

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

    public Result SendBack(Guid financeUserId, string comment)
    {
        if (Status != RequestStatus.PendingFinance)
            return Result.Failure("Send-back is only allowed at the Finance stage.");

        if (string.IsNullOrWhiteSpace(comment))
            return Result.Failure("A comment is required when sending back.");

        var now = DateTime.UtcNow;
        _approvalActions.Add(new ApprovalAction(
            Id, ApprovalStage.Finance, ApprovalDecision.SentBack, financeUserId, comment, actionedAt: now));

        Status = RequestStatus.SentBack;
        return Result.Success();
    }

    public Result ResubmitAfterSendBack(
        Guid departmentId,
        decimal departmentLimit,
        decimal exchangeRateToMmk,
        Guid deptHeadId,
        string deptHeadName,
        string requesterName)
    {
        if (Status != RequestStatus.SentBack)
            return Result.Failure("Only sent-back requests can be resubmitted.");

        // R13: restart the chain. Reuses Submit() — the auto-approval logic re-runs
        // because amount/requester/limit/rate may have changed during the fix.
        return Submit(
            departmentId, departmentLimit, exchangeRateToMmk,
            deptHeadId, deptHeadName, requesterName);
    }
}
