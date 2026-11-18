using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;

namespace SureBudgetRequest.Domain.Entities;

public partial class BudgetRequest
{
    public Result SetAccountingCategory(
        Guid categoryId,
        Guid? actorId = null,
        string? categoryName = null,
        Guid? fallbackUserId = null)
    {
        if (categoryId == Guid.Empty)
            return Result.Failure("Accounting category is required.");

        if (Status is RequestStatus.Draft 
            or RequestStatus.PendingDeptHead 
            or RequestStatus.PendingManagement 
            or RequestStatus.PendingFinance 
            or RequestStatus.Rejected 
            or RequestStatus.Cancelled 
            or RequestStatus.SentBack)
        {
            return Result.Failure("Accounting category can only be assigned after the request is approved.");
        }

        if (FirstPrintedAt.HasValue)
        {
            return Result.Failure(BudgetRequestErrors.AccountingCategoryLockedAfterPrint);
        }

        AccountingCategoryId = categoryId;

        if (actorId.HasValue && actorId.Value != Guid.Empty)
        {
            var comment = !string.IsNullOrWhiteSpace(categoryName)
                ? $"Accounting Category: {categoryName}"
                : "Assigned accounting category";

            _approvalActions.Add(new ApprovalAction(
                Id,
                ApprovalStage.Accounting,
                ApprovalDecision.Categorized,
                actorId.Value,
                comment,
                DateTime.UtcNow,
                fallbackUserId));
        }

        return Result.Success();
    }

    public Result RecordPrint(Guid userId)
    {
        if (userId == Guid.Empty)
            return Result.Failure("User ID is required to record print.");

        if (!FirstPrintedAt.HasValue)
        {
            FirstPrintedAt = DateTime.UtcNow;
            FirstPrintedByUserId = userId;
        }

        return Result.Success();
    }
}
