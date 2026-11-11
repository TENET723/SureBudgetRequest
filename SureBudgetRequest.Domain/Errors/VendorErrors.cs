using SureBudgetRequest.Domain.Common;

namespace SureBudgetRequest.Domain.Errors;

public static class VendorErrors
{
    public static readonly Error GenericNotFound =
        Error.NotFound("Vendor.NotFound", "Vendor not found.");

    public static readonly Error AccountNotFound =
        Error.NotFound("Vendor.AccountNotFound", "Vendor payment account not found.");

    public static Error AlreadyExists(string name) =>
        Error.Conflict("Vendor.AlreadyExists", $"A vendor named '{name}' already exists.");

    public static Error Inactive(string name) =>
        Error.Validation("Vendor.Inactive", $"Vendor '{name}' has been deactivated and cannot be used.");

    public static Error AccountInactive(string provider, string accountNumber) =>
        Error.Validation("Vendor.AccountInactive", $"Payment account '{provider} - {accountNumber}' is inactive.");

    public static readonly Error Required =
        Error.Validation("Vendor.Required", "Vendor is required.");

    public static Error ValidationError(string message) =>
        Error.Validation("Vendor.Validation", message);
}
