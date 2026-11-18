using SureBudgetRequest.Domain.Common;

namespace SureBudgetRequest.Domain.Errors;

public static class AccountingCategoryErrors
{
    public static readonly Error GenericNotFound =
        Error.NotFound("AccountingCategory.NotFound", "Accounting category not found.");

    public static readonly Error NotFound =
        Error.NotFound("AccountingCategory.NotFound", "Selected accounting category not found.");

    public static Error AlreadyExists(string name) =>
        Error.Conflict("AccountingCategory.AlreadyExists", $"Accounting category '{name}' already exists.");

    public static Error Inactive(string name) =>
        Error.Validation("AccountingCategory.Inactive", $"Accounting category '{name}' has been deactivated and cannot be used.");

    public static Error ValidationError(string message) =>
        Error.Validation("AccountingCategory.Validation", message);
}
