using SureBudgetRequest.Domain.Common;

namespace SureBudgetRequest.Domain.Errors;

public static class WithdrawMethodErrors
{
    public static readonly Error GenericNotFound = 
        Error.NotFound("WithdrawMethod.NotFound", "Withdraw method not found.");

    public static readonly Error NotFound = 
        Error.NotFound("WithdrawMethod.NotFound", "Selected withdraw method not found.");

    public static Error AlreadyExists(string name) => 
        Error.Conflict("WithdrawMethod.AlreadyExists", $"Withdraw method '{name}' already exists.");

    public static Error Inactive(string name) => 
        Error.Validation("WithdrawMethod.Inactive", $"Withdraw method '{name}' has been deactivated and cannot be used.");
    
    public static readonly Error Required = 
        Error.Validation("WithdrawMethod.Required", "Withdraw method is required.");

    public static Error ValidationError(string message) =>
        Error.Validation("WithdrawMethod.Validation", message);
}