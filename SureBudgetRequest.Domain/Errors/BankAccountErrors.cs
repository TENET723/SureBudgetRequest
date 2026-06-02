using SureBudgetRequest.Domain.Common;

namespace SureBudgetRequest.Domain.Errors;

public static class BankAccountErrors
{
    public static readonly Error GenericNotFound =
        Error.NotFound("BankAccount.NotFound", "Bank account not found.");

    public static readonly Error NotFound =
        Error.NotFound("BankAccount.NotFound", "Selected bank account not found.");

    public static Error AlreadyExists(string accountNumber) =>
        Error.Conflict("BankAccount.AlreadyExists", $"An active bank account with number '{accountNumber}' already exists.");

    public static Error Inactive(string bankName) =>
        Error.Validation("BankAccount.Inactive", $"Bank account '{bankName}' has been deactivated and cannot be used.");

    public static readonly Error Required =
        Error.Validation("BankAccount.Required", "Bank account is required.");

    public static Error ValidationError(string message) =>
        Error.Validation("BankAccount.Validation", message);
}
