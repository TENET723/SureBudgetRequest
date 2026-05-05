namespace SureBudgetRequest.Domain.Enums;

public enum RequestStatus
{
    Draft = 0,
    PendingDeptHead = 1,
    PendingBoss = 2,
    PendingFinance = 3,
    SentBack = 4,
    Approved = 5,
    PartiallyPaid = 6,
    Paid = 7,
    Rejected = 8,
    Cancelled = 9
}
