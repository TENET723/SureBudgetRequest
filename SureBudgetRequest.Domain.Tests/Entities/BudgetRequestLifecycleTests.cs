using FluentAssertions;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;
using Xunit;

namespace SureBudgetRequest.Domain.Tests.Entities;

public class BudgetRequestLifecycleTests
{
    private static DateTime FutureDate => DateTime.UtcNow.AddHours(6.5).Date.AddDays(2);
    private static DateTime PastDate => DateTime.UtcNow.AddHours(6.5).Date.AddDays(-2);

    private static BudgetRequest CreateStandardDraft(
        Guid? requesterId = null,
        Guid? deptHeadId = null,
        decimal amount = 100_000m,
        string currency = "MMK",
        BudgetRequestType type = BudgetRequestType.Standard,
        string? monthlyJustification = null,
        decimal? manualRate = null)
    {
        return BudgetRequest.CreateDraft(
            requesterId: requesterId ?? Guid.NewGuid(),
            requesterNameAtSubmission: "Alice Requester",
            type: type,
            deptHeadId: deptHeadId ?? Guid.NewGuid(),
            deptHeadName: "Bob DeptHead",
            requestDate: FutureDate,
            requestedAmount: amount,
            currencyCode: currency,
            reasons: "Office Supplies and Equipment",
            withdrawerName: "Alice Withdrawer",
            withdrawerJobTitle: "Lead Engineer",
            withdrawMethodId: Guid.NewGuid(),
            allowsPartialPayment: false,
            partialPaymentDetail: null,
            monthlyOverrunJustification: monthlyJustification,
            manualExchangeRate: manualRate,
            budgetCategoryId: Guid.NewGuid(),
            externalReferences: "PO-9999");
    }

    // =========================================================================
    // 1. CreateDraft Tests & Invariants
    // =========================================================================

    [Fact]
    public void CreateDraft_ValidParameters_ShouldInitializeCorrectly()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var deptHeadId = Guid.NewGuid();
        var withdrawMethodId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var before = DateTime.UtcNow;

        // Act
        var draft = BudgetRequest.CreateDraft(
            requesterId,
            "Alice",
            BudgetRequestType.Advance,
            deptHeadId,
            "Bob",
            FutureDate,
            500_000m,
            "  usd  ",
            "Business Trip to Mandalay",
            "Alice Requester",
            "Senior Engineer",
            withdrawMethodId,
            allowsPartialPayment: true,
            partialPaymentDetail: "50% upfront, 50% on return",
            monthlyOverrunJustification: "  Urgent project deadline  ",
            manualExchangeRate: 3500m,
            budgetCategoryId: categoryId,
            externalReferences: "  REF-12345  ");
        var after = DateTime.UtcNow;

        // Assert
        draft.RequesterId.Should().Be(requesterId);
        draft.RequesterNameAtSubmission.Should().Be("Alice");
        draft.Type.Should().Be(BudgetRequestType.Advance);
        draft.DeptHeadIdAtSubmission.Should().Be(deptHeadId);
        draft.DeptHeadNameAtSubmission.Should().Be("Bob");
        draft.RequestedAmount.Should().Be(500_000m);
        draft.CurrencyCode.Should().Be("USD"); // Upper-cased & trimmed
        draft.ManualExchangeRate.Should().Be(3500m);
        draft.AllowsPartialPayment.Should().BeTrue();
        draft.PartialPaymentDetail.Should().Be("50% upfront, 50% on return");
        draft.MonthlyOverrunJustification.Should().Be("Urgent project deadline"); // Trimmed
        draft.BudgetCategoryId.Should().Be(categoryId);
        draft.ExternalReferences.Should().Be("REF-12345"); // Trimmed
        draft.Status.Should().Be(RequestStatus.Draft);
        draft.ApprovedAmount.Should().Be(0m);
        draft.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void CreateDraft_RequestDateInPast_ShouldThrowArgumentException()
    {
        // Act
        Action act = () => BudgetRequest.CreateDraft(
            Guid.NewGuid(), "Alice", BudgetRequestType.Standard, Guid.NewGuid(), "Bob",
            PastDate, 100m, "MMK", "Reasons", "Alice", "Job", Guid.NewGuid(), false, null);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Request date cannot be in the past.*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.01)]
    [InlineData(-500)]
    public void CreateDraft_InvalidRequestedAmount_ShouldThrowArgumentException(decimal invalidAmount)
    {
        // Act
        Action act = () => BudgetRequest.CreateDraft(
            Guid.NewGuid(), "Alice", BudgetRequestType.Standard, Guid.NewGuid(), "Bob",
            FutureDate, invalidAmount, "MMK", "Reasons", "Alice", "Job", Guid.NewGuid(), false, null);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("requestedAmount")
            .WithMessage("*Requested amount must be greater than zero.*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CreateDraft_InvalidManualExchangeRate_ShouldThrowArgumentException(decimal invalidRate)
    {
        // Act
        Action act = () => BudgetRequest.CreateDraft(
            Guid.NewGuid(), "Alice", BudgetRequestType.Standard, Guid.NewGuid(), "Bob",
            FutureDate, 100m, "USD", "Reasons", "Alice", "Job", Guid.NewGuid(), false, null,
            manualExchangeRate: invalidRate);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("manualExchangeRate")
            .WithMessage("*Manual exchange rate must be greater than zero.*");
    }

    [Fact]
    public void CreateDraft_EmptyWithdrawMethod_ShouldThrowArgumentException()
    {
        // Act
        Action act = () => BudgetRequest.CreateDraft(
            Guid.NewGuid(), "Alice", BudgetRequestType.Standard, Guid.NewGuid(), "Bob",
            FutureDate, 100m, "MMK", "Reasons", "Alice", "Job", Guid.Empty, false, null);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("withdrawMethodId")
            .WithMessage("*Withdraw method is required.*");
    }

    // =========================================================================
    // 2. UpdateDetails Tests
    // =========================================================================

    [Fact]
    public void UpdateDetails_InDraftStatus_ShouldUpdateAndSucceed()
    {
        // Arrange
        var request = CreateStandardDraft();
        var newWithdrawMethod = Guid.NewGuid();

        // Act
        var result = request.UpdateDetails(
            FutureDate.AddDays(1),
            BudgetRequestType.Urgent,
            250_000m,
            "  usd  ",
            "Updated Reason",
            "New Withdrawer",
            "New Job Title",
            newWithdrawMethod,
            allowsPartialPayment: false,
            partialPaymentDetail: null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.Type.Should().Be(BudgetRequestType.Urgent);
        request.RequestedAmount.Should().Be(250_000m);
        request.CurrencyCode.Should().Be("USD");
        request.Reasons.Should().Be("Updated Reason");
        request.WithdrawerName.Should().Be("New Withdrawer");
        request.WithdrawMethodId.Should().Be(newWithdrawMethod);
    }

    [Fact]
    public void UpdateDetails_WhenNotInEditableStatus_ShouldFail()
    {
        // Arrange: Cancel the request so status is Cancelled (non-editable)
        var requesterId = Guid.NewGuid();
        var request = CreateStandardDraft(requesterId: requesterId);
        request.Cancel(requesterId);
        request.Status.Should().Be(RequestStatus.Cancelled);

        // Act
        var result = request.UpdateDetails(
            FutureDate, BudgetRequestType.Standard, 500m, "MMK",
            "Reasons", "Withdrawer", "Job", Guid.NewGuid(), false, null);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("Cannot edit a request in status 'Cancelled'");
    }

    // =========================================================================
    // 3. Submit Method & Routing Invariants
    // =========================================================================

    [Fact]
    public void Submit_NormalEmployee_ShouldRouteToPendingDeptHead()
    {
        // Arrange: Requester != DeptHead
        var requesterId = Guid.NewGuid();
        var deptHeadId = Guid.NewGuid();
        var request = CreateStandardDraft(requesterId, deptHeadId, amount: 100_000m);

        // Act
        var result = request.Submit(
            departmentId: Guid.NewGuid(),
            departmentLimit: 500_000m,
            monthlyLimit: 5_000_000m,
            monthlySpendBeforeInMmk: 0m,
            monthlyWindowStartUtc: DateTime.UtcNow,
            monthlyWindowEndUtc: DateTime.UtcNow.AddMonths(1),
            exchangeRateToMmk: 1m,
            deptHeadId: deptHeadId,
            deptHeadName: "Bob Head",
            requesterName: "Alice Requester",
            withdrawMethodRequiresAttachment: false,
            sequenceNumber: 101);

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.Status.Should().Be(RequestStatus.PendingDeptHead);
        request.Reference.Should().StartWith("BS-");
        request.Reference.Should().EndWith("-101");
        request.SubmittedAt.Should().NotBeNull();
    }

    [Fact]
    public void Submit_DeptHeadSelfSubmitted_UnderLimit_ShouldAutoApproveAndRouteToPendingFinance()
    {
        // Arrange: Requester == DeptHead, amount (200k) <= limit (500k)
        var headId = Guid.NewGuid();
        var request = CreateStandardDraft(requesterId: headId, deptHeadId: headId, amount: 200_000m);

        // Act
        var result = request.Submit(
            departmentId: Guid.NewGuid(),
            departmentLimit: 500_000m,
            monthlyLimit: 5_000_000m,
            monthlySpendBeforeInMmk: 0m,
            monthlyWindowStartUtc: DateTime.UtcNow,
            monthlyWindowEndUtc: DateTime.UtcNow.AddMonths(1),
            exchangeRateToMmk: 1m,
            deptHeadId: headId,
            deptHeadName: "Head Chief",
            requesterName: "Head Chief",
            withdrawMethodRequiresAttachment: false,
            sequenceNumber: 102);

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.Status.Should().Be(RequestStatus.PendingFinance); // Bypasses DeptHead to Finance
        request.ApprovalActions.Should().ContainSingle(a =>
            a.Stage == ApprovalStage.DeptHead && a.Decision == ApprovalDecision.AutoApproved);
    }

    [Fact]
    public void Submit_DeptHeadSelfSubmitted_OverLimit_ShouldAutoApproveDeptHeadAndRouteToPendingManagement()
    {
        // Arrange: Requester == DeptHead, amount (800k) > limit (500k)
        var headId = Guid.NewGuid();
        var request = CreateStandardDraft(requesterId: headId, deptHeadId: headId, amount: 800_000m);

        // Act
        var result = request.Submit(
            departmentId: Guid.NewGuid(),
            departmentLimit: 500_000m,
            monthlyLimit: 5_000_000m,
            monthlySpendBeforeInMmk: 0m,
            monthlyWindowStartUtc: DateTime.UtcNow,
            monthlyWindowEndUtc: DateTime.UtcNow.AddMonths(1),
            exchangeRateToMmk: 1m,
            deptHeadId: headId,
            deptHeadName: "Head Chief",
            requesterName: "Head Chief",
            withdrawMethodRequiresAttachment: false,
            sequenceNumber: 103);

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.Status.Should().Be(RequestStatus.PendingManagement); // Over limit -> Management
        request.ApprovalActions.Should().ContainSingle(a =>
            a.Stage == ApprovalStage.DeptHead && a.Decision == ApprovalDecision.AutoApproved);
    }

    [Theory]
    [InlineData(BudgetRequestType.Standard, "BS")]
    [InlineData(BudgetRequestType.Urgent, "BU")]
    [InlineData(BudgetRequestType.Advance, "BA")]
    [InlineData(BudgetRequestType.ProjectProposal, "BP")]
    public void Submit_ShouldGenerateCorrectReferenceFormatPerType(BudgetRequestType type, string expectedPrefix)
    {
        // Arrange
        var request = CreateStandardDraft(type: type);

        // Act
        var result = request.Submit(
            Guid.NewGuid(), 1_000_000m, 10_000_000m, 0m,
            DateTime.UtcNow, DateTime.UtcNow.AddMonths(1),
            1m, Guid.NewGuid(), "Bob", "Alice", false, 42);

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.Reference.Should().MatchRegex($"^{expectedPrefix}-\\d{{6}}-42$");
    }

    [Fact]
    public void Submit_WhenWithdrawMethodRequiresAttachment_AndNoneProvided_ShouldFail()
    {
        // Arrange
        var request = CreateStandardDraft();

        // Act
        var result = request.Submit(
            Guid.NewGuid(), 1_000_000m, 10_000_000m, 0m,
            DateTime.UtcNow, DateTime.UtcNow.AddMonths(1),
            1m, Guid.NewGuid(), "Bob", "Alice",
            withdrawMethodRequiresAttachment: true, // Requires banking file
            sequenceNumber: 1);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("requires a banking-info attachment");
    }

    [Fact]
    public void Submit_WhenWithdrawMethodRequiresAttachment_AndFilePresent_ShouldSucceed()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var request = CreateStandardDraft(requesterId: requesterId);

        // Add banking file
        request.AddAttachment(
            "bank_book.pdf", "path/bank_book.pdf", "application/pdf", 1024,
            requesterId, AttachmentCategory.WithdrawMethod);

        // Act
        var result = request.Submit(
            Guid.NewGuid(), 1_000_000m, 10_000_000m, 0m,
            DateTime.UtcNow, DateTime.UtcNow.AddMonths(1),
            1m, Guid.NewGuid(), "Bob", "Alice",
            withdrawMethodRequiresAttachment: true,
            sequenceNumber: 1);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Submit_WhenOverMonthlyLimit_WithoutJustification_ShouldFail()
    {
        // Arrange: Spend before (4M) + Request (2M) = 6M > MonthlyLimit (5M)
        var request = CreateStandardDraft(amount: 2_000_000m, monthlyJustification: null);

        // Act
        var result = request.Submit(
            Guid.NewGuid(), 10_000_000m, monthlyLimit: 5_000_000m, monthlySpendBeforeInMmk: 4_000_000m,
            DateTime.UtcNow, DateTime.UtcNow.AddMonths(1),
            1m, Guid.NewGuid(), "Bob", "Alice", false, 1);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("push the department over its monthly budget limit. A justification is required.");
    }

    [Fact]
    public void Submit_WhenOverMonthlyLimit_WithJustification_ShouldSucceed()
    {
        // Arrange: Over monthly limit, but justification is supplied
        var request = CreateStandardDraft(
            amount: 2_000_000m,
            monthlyJustification: "Special Q3 marketing campaign expansion");

        // Act
        var result = request.Submit(
            Guid.NewGuid(), 10_000_000m, monthlyLimit: 5_000_000m, monthlySpendBeforeInMmk: 4_000_000m,
            DateTime.UtcNow, DateTime.UtcNow.AddMonths(1),
            1m, Guid.NewGuid(), "Bob", "Alice", false, 1);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    // =========================================================================
    // 4. Cancel Tests
    // =========================================================================

    [Fact]
    public void Cancel_ByRequester_InDraftOrPendingDeptHead_ShouldSucceed()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var request = CreateStandardDraft(requesterId: requesterId);

        // Act
        var result = request.Cancel(requesterId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.Status.Should().Be(RequestStatus.Cancelled);
        request.FinalizedAt.Should().NotBeNull();
    }

    [Fact]
    public void Cancel_ByDifferentUser_ShouldFail()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var otherUser = Guid.NewGuid();
        var request = CreateStandardDraft(requesterId: requesterId);

        // Act
        var result = request.Cancel(otherUser);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Be("Only the requester can cancel their request.");
        request.Status.Should().Be(RequestStatus.Draft);
    }

    // =========================================================================
    // 5. Attachments Invariants & Removal
    // =========================================================================

    [Fact]
    public void AddAttachment_ExceedingMaximumAllowedPerCategory_ShouldFail()
    {
        // Arrange: Max 10 general attachments allowed
        var requesterId = Guid.NewGuid();
        var request = CreateStandardDraft(requesterId: requesterId);

        for (var i = 1; i <= 10; i++)
        {
            var addResult = request.AddAttachment(
                $"file{i}.pdf", $"path/file{i}.pdf", "application/pdf", 1024, requesterId, AttachmentCategory.General);
            addResult.IsSuccess.Should().BeTrue();
        }

        // Act: 11th attachment
        var eleventhResult = request.AddAttachment(
            "file11.pdf", "path/file11.pdf", "application/pdf", 1024, requesterId, AttachmentCategory.General);

        // Assert
        eleventhResult.IsFailure.Should().BeTrue();
        eleventhResult.Error.Message.Should().Contain("Cannot add more than 10 General attachments");
    }

    [Fact]
    public void RemoveAttachment_ValidRequester_ShouldRemoveAndReturnPath()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var request = CreateStandardDraft(requesterId: requesterId);
        request.AddAttachment("receipt.pdf", "storage/receipt.pdf", "application/pdf", 2048, requesterId);
        var attachmentId = request.Attachments.Single().Id;

        // Act
        var removeResult = request.RemoveAttachment(attachmentId, requesterId);

        // Assert
        removeResult.IsSuccess.Should().BeTrue();
        removeResult.Value.Should().Be("storage/receipt.pdf");
        request.Attachments.Should().BeEmpty();
    }

    [Fact]
    public void ClearWithdrawMethodAttachments_ShouldPurgeOnlyBankingFiles_AndReturnPaths()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var request = CreateStandardDraft(requesterId: requesterId);
        request.AddAttachment("gen.pdf", "path/gen.pdf", "application/pdf", 1024, requesterId, AttachmentCategory.General);
        request.AddAttachment("bank1.png", "path/bank1.png", "image/png", 1024, requesterId, AttachmentCategory.WithdrawMethod);
        request.AddAttachment("bank2.png", "path/bank2.png", "image/png", 1024, requesterId, AttachmentCategory.WithdrawMethod);

        // Act
        var clearedPaths = request.ClearWithdrawMethodAttachments();

        // Assert
        clearedPaths.Should().BeEquivalentTo(new[] { "path/bank1.png", "path/bank2.png" });
        request.Attachments.Should().ContainSingle(a => a.Category == AttachmentCategory.General);
    }
}
