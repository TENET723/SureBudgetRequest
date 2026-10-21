using FluentAssertions;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;
using Xunit;

namespace SureBudgetRequest.Domain.Tests.Entities;

public class BudgetRequestPaymentsTests
{
    private static BudgetRequest CreateApprovedRequest(
        decimal amount = 100_000m,
        BudgetRequestType type = BudgetRequestType.Standard,
        bool allowsPartialPayment = false)
    {
        var reqId = Guid.NewGuid();
        var headId = Guid.NewGuid();
        var request = BudgetRequest.CreateDraft(
            reqId, "Alice", type, headId, "Bob Head",
            DateTime.UtcNow.AddHours(6.5).Date.AddDays(2),
            amount, "MMK", "Reasons", "Alice", "Job", Guid.NewGuid(),
            allowsPartialPayment, allowsPartialPayment ? "Partial details" : null);

        request.Submit(
            Guid.NewGuid(), 500_000m, 10_000_000m, 0m,
            DateTime.UtcNow, DateTime.UtcNow.AddMonths(1),
            1m, headId, "Bob Head", "Alice", false, 1);

        request.ApproveBy(headId);

        var coaId = Guid.NewGuid();
        var deadline = type == BudgetRequestType.Advance ? DateTime.UtcNow.AddDays(14) : (DateTime?)null;
        request.ApproveBy(Guid.NewGuid(), coaId, deadline);

        return request;
    }

    // =========================================================================
    // 1. Status & Parameter Invariants
    // =========================================================================

    [Theory]
    [InlineData(0)]
    [InlineData(-0.01)]
    [InlineData(-100_000)]
    public void RecordPayment_ZeroOrNegativeAmount_ShouldFail(decimal invalidAmount)
    {
        // Arrange
        var request = CreateApprovedRequest(100_000m);

        // Act
        var result = request.RecordPayment(invalidAmount, DateTime.UtcNow, Guid.NewGuid(), "REF", "Note");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Be("Payment amount must be greater than zero.");
        request.Payments.Should().BeEmpty();
    }

    [Fact]
    public void RecordPayment_InUnapprovedStatus_ShouldFail()
    {
        // Arrange: In Draft status
        var request = BudgetRequest.CreateDraft(
            Guid.NewGuid(), "Alice", BudgetRequestType.Standard, Guid.NewGuid(), "Bob",
            DateTime.UtcNow.AddHours(6.5).Date.AddDays(2),
            100_000m, "MMK", "Reasons", "Alice", "Job", Guid.NewGuid(), false, null);

        // Act
        var result = request.RecordPayment(100_000m, DateTime.UtcNow, Guid.NewGuid(), "REF", "Note");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("Cannot record payments while status is 'Draft'");
    }

    // =========================================================================
    // 2. Date Invariants
    // =========================================================================

    [Fact]
    public void RecordPayment_PaymentDateBeforeApprovalDate_ShouldFail()
    {
        // Arrange
        var request = CreateApprovedRequest(100_000m);
        var pastDateBeforeApproval = DateTime.UtcNow.AddDays(-10);

        // Act
        var result = request.RecordPayment(100_000m, pastDateBeforeApproval, Guid.NewGuid(), "REF", "Note");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Be("Payment date cannot be before the request's approval date.");
    }

    [Fact]
    public void RecordPayment_PaymentDateInFuture_ShouldFail()
    {
        // Arrange
        var request = CreateApprovedRequest(100_000m);
        var farFutureDate = DateTime.UtcNow.AddHours(2);

        // Act
        var result = request.RecordPayment(100_000m, farFutureDate, Guid.NewGuid(), "REF", "Note");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Be("Payment date cannot be in the future.");
    }

    // =========================================================================
    // 3. Partial vs Full Payment Rules
    // =========================================================================

    [Fact]
    public void RecordPayment_WhenPartialNotAllowed_AndAmountIsNotFullApproved_ShouldFail()
    {
        // Arrange: allowsPartialPayment = false, ApprovedAmount = 100k
        var request = CreateApprovedRequest(100_000m, allowsPartialPayment: false);

        // Act: Attempting 50k payment
        var result = request.RecordPayment(50_000m, DateTime.UtcNow, Guid.NewGuid(), "REF", "Note");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("does not allow partial payments. The full approved amount must be paid in one transaction.");
        request.Status.Should().Be(RequestStatus.Approved);
    }

    [Fact]
    public void RecordPayment_WhenPartialAllowed_ShouldSupportMultipleIncrementalPayments()
    {
        // Arrange: allowsPartialPayment = true, ApprovedAmount = 100k
        var request = CreateApprovedRequest(100_000m, allowsPartialPayment: true);
        var financeUser = Guid.NewGuid();

        // Act 1: First tranche of 40k
        var result1 = request.RecordPayment(40_000m, DateTime.UtcNow, financeUser, "TR1", "First Tranche");
        result1.IsSuccess.Should().BeTrue();
        request.Status.Should().Be(RequestStatus.PartiallyPaid);
        request.TotalPaid.Should().Be(40_000m);
        request.RemainingBalance.Should().Be(60_000m);

        // Act 2: Second tranche of 60k (completing the payment)
        var result2 = request.RecordPayment(60_000m, DateTime.UtcNow, financeUser, "TR2", "Second Tranche");
        result2.IsSuccess.Should().BeTrue();
        request.Status.Should().Be(RequestStatus.Paid);
        request.TotalPaid.Should().Be(100_000m);
        request.RemainingBalance.Should().Be(0m);
        request.FinalizedAt.Should().NotBeNull();
    }

    [Fact]
    public void RecordPayment_ExceedingApprovedBalance_ShouldFail()
    {
        // Arrange: allowsPartialPayment = true, ApprovedAmount = 100k
        var request = CreateApprovedRequest(100_000m, allowsPartialPayment: true);
        var financeUser = Guid.NewGuid();
        request.RecordPayment(80_000m, DateTime.UtcNow, financeUser, "P1", "Tranche 1");

        // Act: Attempting to pay 30k when remaining is 20k
        var result = request.RecordPayment(30_000m, DateTime.UtcNow, financeUser, "P2", "Overpayment");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("Payment exceeds remaining balance. Remaining: 20000, attempted: 30000.");
        request.TotalPaid.Should().Be(80_000m);
    }

    // =========================================================================
    // 4. Status Auto-Transitions (Standard vs Advance Request)
    // =========================================================================

    [Fact]
    public void RecordPayment_StandardRequest_FullPayment_ShouldTransitionToPaidAndFinalize()
    {
        // Arrange: Standard request for 500k
        var request = CreateApprovedRequest(500_000m, BudgetRequestType.Standard);
        var before = DateTime.UtcNow;

        // Act
        var result = request.RecordPayment(
            500_000m, DateTime.UtcNow, Guid.NewGuid(), "CHQ-001", "Full payment in cash");
        var after = DateTime.UtcNow;

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.Status.Should().Be(RequestStatus.Paid);
        request.FinalizedAt.Should().NotBeNull();
        request.FinalizedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void RecordPayment_AdvanceRequest_FullPayment_ShouldTransitionToPendingReconciliation_NotPaid()
    {
        // Arrange: Advance request for 500k
        var request = CreateApprovedRequest(500_000m, BudgetRequestType.Advance);

        // Act
        var result = request.RecordPayment(
            500_000m, DateTime.UtcNow, Guid.NewGuid(), "ADV-001", "Full advance disbursement");

        // Assert: Advances move to PendingReconciliation, FinalizedAt remains null until settlement
        result.IsSuccess.Should().BeTrue();
        request.Status.Should().Be(RequestStatus.PendingReconciliation);
        request.FinalizedAt.Should().BeNull();
    }

    // =========================================================================
    // 5. Source Bank Account & Metadata Capture
    // =========================================================================

    [Fact]
    public void RecordPayment_WithSourceBankMetadata_ShouldStoreOnPaymentRecord()
    {
        // Arrange
        var request = CreateApprovedRequest(100_000m);
        var bankAccountId = Guid.NewGuid();
        var financeUser = Guid.NewGuid();

        // Act
        var result = request.RecordPayment(
            100_000m,
            DateTime.UtcNow,
            financeUser,
            reference: "TXN-987654",
            note: "Bank transfer via KBZ",
            attachmentIds: null,
            sourceBankAccountId: bankAccountId,
            sourceBankName: "KBZ Bank",
            sourceAccountNumber: "0123456789",
            sourceAccountHolderName: "Company Payroll");

        // Assert
        result.IsSuccess.Should().BeTrue();
        var payment = request.Payments.Single();
        payment.Amount.Should().Be(100_000m);
        payment.RecordedByUserId.Should().Be(financeUser);
        payment.Reference.Should().Be("TXN-987654");
        payment.Note.Should().Be("Bank transfer via KBZ");
        payment.SourceBankAccountId.Should().Be(bankAccountId);
        payment.SourceBankName.Should().Be("KBZ Bank");
        payment.SourceAccountNumber.Should().Be("0123456789");
        payment.SourceAccountHolderName.Should().Be("Company Payroll");
    }
}
