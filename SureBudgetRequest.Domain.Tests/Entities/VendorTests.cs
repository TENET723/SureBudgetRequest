using FluentAssertions;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;
using Xunit;

namespace SureBudgetRequest.Domain.Tests.Entities;

public class VendorTests
{
    [Fact]
    public void Constructor_ValidParameters_ShouldInitializeCorrectly()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var vendor = new Vendor(
            "  Apex Office Supplies  ",
            "  U Aye  ",
            "  09123456789  ",
            "  apex@example.com  ",
            "  No. 12, Main Road, Yangon  ");

        var after = DateTime.UtcNow;

        // Assert
        vendor.Name.Should().Be("Apex Office Supplies");
        vendor.ContactPerson.Should().Be("U Aye");
        vendor.Phone.Should().Be("09123456789");
        vendor.Email.Should().Be("apex@example.com");
        vendor.Address.Should().Be("No. 12, Main Road, Yangon");
        vendor.IsActive.Should().BeTrue();
        vendor.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        vendor.PaymentAccounts.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_InvalidName_ShouldThrowArgumentException(string? invalidName)
    {
        // Act
        Action act = () => new Vendor(invalidName!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("name");
    }

    [Fact]
    public void UpdateDetails_ShouldUpdateFieldsProperly()
    {
        // Arrange
        var vendor = new Vendor("Original Name");

        // Act
        vendor.UpdateDetails("Updated Name", "New Contact", "09987654321", null, "New Address");

        // Assert
        vendor.Name.Should().Be("Updated Name");
        vendor.ContactPerson.Should().Be("New Contact");
        vendor.Phone.Should().Be("09987654321");
        vendor.Email.Should().BeNull();
        vendor.Address.Should().Be("New Address");
    }

    [Fact]
    public void DeactivateAndReactivate_ShouldToggleIsActive()
    {
        // Arrange
        var vendor = new Vendor("Vendor A");

        // Act & Assert
        vendor.Deactivate();
        vendor.IsActive.Should().BeFalse();

        vendor.Reactivate();
        vendor.IsActive.Should().BeTrue();
    }

    [Fact]
    public void AddPaymentAccount_FirstAccount_ShouldAutomaticallyBeDefault()
    {
        // Arrange
        var vendor = new Vendor("Vendor KBZ");

        // Act
        var account = vendor.AddPaymentAccount(
            PaymentAccountType.MobileWallet,
            "KBZPay",
            "09123456789",
            "U Kyaw",
            branchName: null,
            isDefault: false); // even if passed false, first active account becomes default

        // Assert
        account.IsDefault.Should().BeTrue();
        account.AccountType.Should().Be(PaymentAccountType.MobileWallet);
        account.ProviderName.Should().Be("KBZPay");
        account.AccountNumber.Should().Be("09123456789");
        account.AccountHolderName.Should().Be("U Kyaw");
        account.IsActive.Should().BeTrue();
        vendor.PaymentAccounts.Should().ContainSingle();
    }

    [Fact]
    public void AddPaymentAccount_SecondAccountAsDefault_ShouldMakePreviousNonDefault()
    {
        // Arrange
        var vendor = new Vendor("Vendor Multi");
        var first = vendor.AddPaymentAccount(PaymentAccountType.MobileWallet, "KBZPay", "09111", "Name 1");

        // Act
        var second = vendor.AddPaymentAccount(
            PaymentAccountType.BankTransfer,
            "KBZ Bank",
            "0123456789",
            "Name 2",
            branchName: "Hledan",
            isDefault: true);

        // Assert
        first.IsDefault.Should().BeFalse();
        second.IsDefault.Should().BeTrue();
        vendor.PaymentAccounts.Should().HaveCount(2);
    }

    [Fact]
    public void SetDefaultPaymentAccount_ShouldSwitchDefaultCorrectly()
    {
        // Arrange
        var vendor = new Vendor("Vendor Test");
        var acc1 = vendor.AddPaymentAccount(PaymentAccountType.MobileWallet, "KBZPay", "09111", "Name 1");
        var acc2 = vendor.AddPaymentAccount(PaymentAccountType.BankTransfer, "AYA Bank", "09222", "Name 2", isDefault: false);

        acc1.IsDefault.Should().BeTrue();
        acc2.IsDefault.Should().BeFalse();

        // Act
        vendor.SetDefaultPaymentAccount(acc2.Id);

        // Assert
        acc1.IsDefault.Should().BeFalse();
        acc2.IsDefault.Should().BeTrue();
    }

    [Fact]
    public void DeactivatePaymentAccount_WhenDefault_ShouldElectNextActiveAccountAsDefault()
    {
        // Arrange
        var vendor = new Vendor("Vendor Deact");
        var acc1 = vendor.AddPaymentAccount(PaymentAccountType.MobileWallet, "KBZPay", "09111", "Name 1", isDefault: true);
        var acc2 = vendor.AddPaymentAccount(PaymentAccountType.BankTransfer, "CB Bank", "09222", "Name 2", isDefault: false);

        // Act
        vendor.DeactivatePaymentAccount(acc1.Id);

        // Assert
        acc1.IsActive.Should().BeFalse();
        acc1.IsDefault.Should().BeFalse();
        acc2.IsDefault.Should().BeTrue();
    }

    [Fact]
    public void SetAccountAttachment_ShouldSetAttachmentProperties()
    {
        // Arrange
        var vendor = new Vendor("Vendor Att");
        var acc = vendor.AddPaymentAccount(PaymentAccountType.MobileWallet, "KBZPay", "09111", "Name 1");

        // Act
        vendor.SetAccountAttachment(acc.Id, "qr.png", "vendors/123/qr.png", "image/png", 1024);

        // Assert
        acc.HasAttachment.Should().BeTrue();
        acc.AttachmentFileName.Should().Be("qr.png");
        acc.AttachmentStoredPath.Should().Be("vendors/123/qr.png");
        acc.AttachmentContentType.Should().Be("image/png");
        acc.AttachmentSizeBytes.Should().Be(1024);

        // Act: remove attachment
        vendor.RemoveAccountAttachment(acc.Id);

        // Assert
        acc.HasAttachment.Should().BeFalse();
        acc.AttachmentFileName.Should().BeNull();
        acc.AttachmentStoredPath.Should().BeNull();
    }
}
