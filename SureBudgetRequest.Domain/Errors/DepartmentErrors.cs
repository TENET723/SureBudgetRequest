using SureBudgetRequest.Domain.Common;

namespace SureBudgetRequest.Domain.Errors;

public static class DepartmentErrors
{
    public static readonly Error NotFound = 
        Error.NotFound("Department.NotFound", "Department not found.");

    public static readonly Error HeadNotFound = 
        Error.NotFound("Department.HeadNotFound", "Department head user not found.");

    public static Error ValidationError(string message) =>
        Error.Validation("Department.Validation", message);
}
