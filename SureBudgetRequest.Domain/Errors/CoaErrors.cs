using SureBudgetRequest.Domain.Common;

namespace SureBudgetRequest.Domain.Errors;

public static class CoaErrors
{
    public static readonly Error NotFound = 
        Error.NotFound("Coa.NotFound", "Chart of Account not found.");

    public static Error AlreadyExists(string code) => 
        Error.Conflict("Coa.AlreadyExists", $"Chart of Account with code '{code}' already exists.");

    public static Error ValidationError(string message) =>
        Error.Validation("Coa.Validation", message);
}
