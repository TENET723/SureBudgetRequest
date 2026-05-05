using SureBudgetRequest.Domain.Enums;

namespace SureBudgetRequest.Domain.Entities;

public class ApprovalAction
{
    public Guid Id { get; private set; }
    public Guid BudgetRequestId { get; private set; }
    public ApprovalStage Stage { get; private set; }
    public ApprovalDecision Decision { get; private set; }
    public Guid ApproverId { get; private set; }
    public string? Comment { get; private set; }
    public DateTime ActionedAt { get; private set; }

    // For EF Core
    private ApprovalAction() { }

    // Internal: only Domain code (BudgetRequest) can create approval actions.
    // Application/Web layers cannot instantiate this directly.
    internal ApprovalAction(
        Guid budgetRequestId,
        ApprovalStage stage,
        ApprovalDecision decision,
        Guid approverId,
        string? comment,
        DateTime actionedAt)
    {
        Id = Guid.NewGuid();
        BudgetRequestId = budgetRequestId;
        Stage = stage;
        Decision = decision;
        ApproverId = approverId;
        Comment = comment;
        ActionedAt = actionedAt;
    }
}
