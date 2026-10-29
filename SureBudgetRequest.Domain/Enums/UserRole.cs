namespace SureBudgetRequest.Domain.Enums;

public enum UserRole
{
    Employee = 0,
    DepartmentHead = 1,
    Management = 2,
    Finance = 3,
    Admin = 4,

    /// <summary>
    /// Accounting role: sees everything Finance can see. Read-only for all financial and approval actions
    /// (approvals, payments, refunds, reconciliations), but has write access to ticket tag annotations for audit and record-keeping.
    /// </summary>
    Accounting = 5,

    /// <summary>
    /// Support role: can create, update, and deactivate users only.
    /// Cannot submit budget requests or access any other part of the system.
    /// </summary>
    Support = 6
}
