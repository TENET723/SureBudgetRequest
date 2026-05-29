using SureBudgetRequest.Domain.Common;

namespace SureBudgetRequest.Domain.Errors;

public static class BudgetCategoryErrors
{
    public static readonly Error GenericNotFound =
        Error.NotFound("BudgetCategory.NotFound", "Budget category not found.");

    public static readonly Error NotFound =
        Error.NotFound("BudgetCategory.NotFound", "Selected budget category not found.");

    public static Error AlreadyExists(string name) =>
        Error.Conflict("BudgetCategory.AlreadyExists", $"Budget category '{name}' already exists.");

    public static Error Inactive(string name) =>
        Error.Validation("BudgetCategory.Inactive", $"Budget category '{name}' has been deactivated and cannot be used.");

    public static Error ValidationError(string message) =>
        Error.Validation("BudgetCategory.Validation", message);
}
