namespace SureBudgetRequest.Domain.Enums;

/// <summary>
/// Classifies the type of destination payment account for a vendor.
/// </summary>
public enum PaymentAccountType
{
    BankTransfer = 0,
    MobileWallet = 1,
    Other = 2
}
