using SureBudgetRequest.Domain.Common;

namespace SureBudgetRequest.Domain.Errors;

public static class BudgetRequestErrors
{
    public static Error NotFound(Guid id) => 
        Error.NotFound("BudgetRequest.NotFound", $"Budget request with ID '{id}' was not found.");

    public static readonly Error OnlyFinanceCanExtend = 
        Error.Forbidden("BudgetRequest.OnlyFinanceCanExtend", "Only a Finance user can extend a reconciliation deadline.");

    public static readonly Error OnlyFinanceCanRecordPayment = 
        Error.Forbidden("BudgetRequest.OnlyFinanceCanRecordPayment", "Only a Finance user can record payments.");

    public static readonly Error CoaRequiredForFinanceApproval = 
        Error.Validation("BudgetRequest.CoaRequiredForFinanceApproval", "Chart of Account is required when Finance approves.");

    public static readonly Error CoaNotFound = 
        Error.NotFound("BudgetRequest.CoaNotFound", "The selected Chart of Account no longer exists.");

    public static Error CoaDeactivated(string code) => 
        Error.Validation("BudgetRequest.CoaDeactivated", $"Chart of Account '{code}' has been deactivated and cannot be used.");

    public static readonly Error OnlyFinanceCanRecordRefund =
        Error.Forbidden("BudgetRequest.OnlyFinanceCanRecordRefund", "Only a Finance user can record a refund.");

    public static readonly Error OnlyFinanceCanRecordReimbursement =
        Error.Forbidden("BudgetRequest.OnlyFinanceCanRecordReimbursement", "Only a Finance user can record a reimbursement.");

    public static readonly Error FinanceApproverRequired = 
        Error.Forbidden("BudgetRequest.FinanceApproverRequired", "This Finance user is restricted to recording payments and cannot perform this action.");

    public static readonly Error OnlyRequesterCanRemoveUsage = 
        Error.Forbidden("BudgetRequest.OnlyRequesterCanRemoveUsage", "Only the requester can remove usage from their own advance.");

    public static readonly Error OnlyRequesterCanResubmit = 
        Error.Forbidden("BudgetRequest.OnlyRequesterCanResubmit", "Only the requester can resubmit their request.");

    public static readonly Error DepartmentNotFound = 
        Error.NotFound("BudgetRequest.DepartmentNotFound", "Requester's department not found.");

    public static readonly Error DepartmentHeadNotAssigned = 
        Error.Validation("BudgetRequest.DepartmentHeadNotAssigned", "Your department has no head assigned. Contact admin before proceeding.");

    public static readonly Error DepartmentHeadNotFound = 
        Error.NotFound("BudgetRequest.DepartmentHeadNotFound", "Department head not found.");

    public static readonly Error OnlyRequesterCanSubmitReconciliation = 
        Error.Forbidden("BudgetRequest.OnlyRequesterCanSubmitReconciliation", "Only the requester can submit reconciliation for their own advance.");

    public static readonly Error OnlyRequesterCanSubmit = 
        Error.Forbidden("BudgetRequest.OnlyRequesterCanSubmit", "Only the requester can submit their request.");

    public static readonly Error OnlyRequesterCanUpdateUsage = 
        Error.Forbidden("BudgetRequest.OnlyRequesterCanUpdateUsage", "Only the requester can edit usage on their own advance.");

    public static readonly Error OnlyRequesterCanEditDraft = 
        Error.Forbidden("BudgetRequest.OnlyRequesterCanEditDraft", "Only the requester can edit their draft.");

    public static readonly Error AdvanceSubmissionBlockedMonthEnd = 
        Error.Validation("BudgetRequest.AdvanceSubmissionBlockedMonthEnd", "Advance requests cannot be submitted during the month-end blackout window.");
}