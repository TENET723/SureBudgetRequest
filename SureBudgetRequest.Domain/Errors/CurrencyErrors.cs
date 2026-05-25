using SureBudgetRequest.Domain.Common;

namespace SureBudgetRequest.Domain.Errors;

public static class CurrencyErrors
{
    public static readonly Error GenericNotFound = 
        Error.NotFound("Currency.NotFound", "Currency not found.");

    public static Error NotFound(string code) => 
        Error.NotFound("Currency.NotFound", $"Currency '{code}' was not found.");

    public static Error AlreadyExists(string code) => 
        Error.Conflict("Currency.AlreadyExists", $"Currency '{code}' already exists.");

    public static Error Inactive(string code) => 
        Error.Validation("Currency.Inactive", $"Currency '{code}' is not active.");

    public static Error ValidationError(string message) =>
        Error.Validation("Currency.Validation", message);
}