namespace SureBudgetRequest.Domain.Enums;

public enum UserRole
{
    Employee = 0,
    DepartmentHead = 1,
    Management = 2,
    Finance = 3,
    Admin = 4,

    /// <summary>
    /// Read-only role: sees everything Finance can see but can take no actions.
    /// Read-only is enforced at the command boundary — no command handler gates on
    /// this value, so every write path fails for Accounting by construction.
    /// </summary>
    Accounting = 5
}
