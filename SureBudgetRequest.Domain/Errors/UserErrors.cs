using SureBudgetRequest.Domain.Common;

namespace SureBudgetRequest.Domain.Errors;

public static class UserErrors
{
    public static readonly Error GenericNotFound = 
        Error.NotFound("User.NotFound", "User not found.");

    public static Error NotFound(Guid id) => 
        Error.NotFound("User.NotFound", $"User with ID '{id}' was not found.");

    public static readonly Error NotFoundOrInactive = 
        Error.NotFound("User.NotFoundOrInactive", "User not found or is inactive.");

    public static readonly Error InvalidCredentials = 
        Error.Unauthorized("User.InvalidCredentials", "Invalid email or password.");

    public static readonly Error Inactive = 
        Error.Validation("User.Inactive", "Inactive users cannot perform this action.");

    public static readonly Error EmailAlreadyExists = 
        Error.Conflict("User.EmailAlreadyExists", "A user with this email already exists.");

    public static Error PasswordTooShort(int minLength) =>
        Error.Validation("User.PasswordTooShort", $"Password must be at least {minLength} characters.");

    public static readonly Error PasswordsMustBeDifferent =
        Error.Validation("User.PasswordsMustBeDifferent", "New password must be different from the current password.");

    public static readonly Error CurrentPasswordIncorrect =
        Error.Validation("User.CurrentPasswordIncorrect", "Current password is incorrect.");

    public static readonly Error EmailRequired =
        Error.Validation("User.EmailRequired", "Email is required.");

    public static readonly Error OnlyFinanceCanBeApprover = 
        Error.Validation("User.OnlyFinanceCanBeApprover", "Only a user with the Finance role can be marked as a Finance Approver.");

    public static readonly Error LastActiveFinanceApprover = 
        Error.Validation("User.LastActiveFinanceApprover", "Cannot deactivate or remove the last active Finance Approver — Finance-stage requests would have no one to approve them. Promote another Finance user first.");

    public static Error RoleUnauthorized(string role, string status) => 
        Error.Forbidden("User.RoleUnauthorized", $"User with role '{role}' cannot perform this action at the current stage '{status}'.");

    public static readonly Error NoUserSignedIn = 
        Error.Unauthorized("User.NoUserSignedIn", "No user signed in.");

    public static Error ValidationError(string message) =>
        Error.Validation("User.Validation", message);
}