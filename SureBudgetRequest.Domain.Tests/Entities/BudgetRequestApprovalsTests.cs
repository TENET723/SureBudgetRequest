using FluentAssertions;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;
using Xunit;

namespace SureBudgetRequest.Domain.Tests.Entities;

public class BudgetRequestApprovalsTests
{
    private static DateTime FutureDeadline => DateTime.UtcNow.AddDays(14);
    private static DateTime PastDeadline => DateTime.UtcNow.AddDays(-1);

    private static BudgetRequest CreateSubmittedRequest(
        decimal amount = 100_000m,
        decimal deptLimit = 500_000m,
        BudgetRequestType type = BudgetRequestType.Standard,
        Guid? requesterId = null,
        Guid? deptHeadId = null)
    {
        var reqId = requesterId ?? Guid.NewGuid();
        var headId = deptHeadId ?? Guid.NewGuid();
        var request = BudgetRequest.CreateDraft(
            reqId, "Alice", type, headId, "Bob Head",
            DateTime.UtcNow.AddHours(6.5).Date.AddDays(2),
            amount, "MMK", "Reasons", "Alice", "Job", Guid.NewGuid(), false, null);

        request.Submit(
            Guid.NewGuid(), deptLimit, 10_000_000m, 0m,
            DateTime.UtcNow, DateTime.UtcNow.AddMonths(1),
            1m, headId, "Bob Head", "Alice", false, 100);

        return request;
    }

    // =========================================================================
    // 1. ApproveBy Tests (DeptHead, Management, Finance Stages)
    // =========================================================================

    [Fact]
    public void ApproveBy_DeptHeadStage_UnderLimit_ShouldRouteToPendingFinance()
    {
        // Arrange: Amount (100k) <= DeptLimit (500k)
        var headId = Guid.NewGuid();
        var request = CreateSubmittedRequest(amount: 100_000m, deptLimit: 500_000m, deptHeadId: headId);
        request.Status.Should().Be(RequestStatus.PendingDeptHead);

        // Act
        var result = request.ApproveBy(headId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.Status.Should().Be(RequestStatus.PendingFinance); // Under limit bypasses Management
        request.ApprovalActions.Should().ContainSingle(a =>
            a.Stage == ApprovalStage.DeptHead && a.Decision == ApprovalDecision.Approved && a.ApproverId == headId);
    }

    [Fact]
    public void ApproveBy_DeptHeadStage_OverLimit_ShouldRouteToPendingManagement()
    {
        // Arrange: Amount (800k) > DeptLimit (500k)
        var headId = Guid.NewGuid();
        var request = CreateSubmittedRequest(amount: 800_000m, deptLimit: 500_000m, deptHeadId: headId);
        request.Status.Should().Be(RequestStatus.PendingDeptHead);

        // Act
        var result = request.ApproveBy(headId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.Status.Should().Be(RequestStatus.PendingManagement); // Over limit -> Management
        request.ApprovalActions.Should().ContainSingle(a =>
            a.Stage == ApprovalStage.DeptHead && a.Decision == ApprovalDecision.Approved);
    }

    [Fact]
    public void ApproveBy_DeptHeadStage_ByUnassignedApprover_ShouldFail()
    {
        // Arrange
        var headId = Guid.NewGuid();
        var wrongApprover = Guid.NewGuid();
        var request = CreateSubmittedRequest(deptHeadId: headId);

        // Act
        var result = request.ApproveBy(wrongApprover);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Be("This user is not the assigned approver for the current stage.");
        request.Status.Should().Be(RequestStatus.PendingDeptHead);
    }

    [Fact]
    public void ApproveBy_DeptHeadStage_ByRequester_ShouldFail()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        // Force DeptHeadIdAtSubmission to match requesterId on a manual approval attempt
        var request = CreateSubmittedRequest(requesterId: requesterId, deptHeadId: Guid.NewGuid());
        // Attempting manual approval using requesterId
        var result = request.ApproveBy(requesterId);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ApproveBy_ManagementStage_ShouldRouteToPendingFinance()
    {
        // Arrange: Push to Management stage
        var headId = Guid.NewGuid();
        var request = CreateSubmittedRequest(amount: 800_000m, deptLimit: 500_000m, deptHeadId: headId);
        request.ApproveBy(headId);
        request.Status.Should().Be(RequestStatus.PendingManagement);

        var managementApprover = Guid.NewGuid();

        // Act
        var result = request.ApproveBy(managementApprover);

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.Status.Should().Be(RequestStatus.PendingFinance);
        request.ApprovalActions.Should().Contain(a =>
            a.Stage == ApprovalStage.Management && a.Decision == ApprovalDecision.Approved);
    }

    [Fact]
    public void ApproveBy_FinanceStage_StandardRequest_WithValidCoa_ShouldApprove()
    {
        // Arrange: Standard request moved to Finance
        var headId = Guid.NewGuid();
        var request = CreateSubmittedRequest(amount: 100_000m, deptLimit: 500_000m, deptHeadId: headId);
        request.ApproveBy(headId);
        request.Status.Should().Be(RequestStatus.PendingFinance);

        var financeApprover = Guid.NewGuid();
        var coaId = Guid.NewGuid();

        // Act
        var result = request.ApproveBy(financeApprover, coaId: coaId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.Status.Should().Be(RequestStatus.Approved);
        request.ApprovedAmount.Should().Be(100_000m);
        request.CoaId.Should().Be(coaId);
        request.ReconciliationDeadline.Should().BeNull();
        request.ApprovalActions.Should().Contain(a =>
            a.Stage == ApprovalStage.Finance && a.Decision == ApprovalDecision.Approved);
    }

    [Fact]
    public void ApproveBy_FinanceStage_MissingCoa_ShouldFail()
    {
        // Arrange
        var headId = Guid.NewGuid();
        var request = CreateSubmittedRequest(deptHeadId: headId);
        request.ApproveBy(headId);
        request.Status.Should().Be(RequestStatus.PendingFinance);

        // Act: Missing COA
        var result = request.ApproveBy(Guid.NewGuid(), coaId: null);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Be("Chart of Account is required when Finance approves.");
    }

    [Fact]
    public void ApproveBy_FinanceStage_AdvanceRequest_RequiresFutureReconciliationDeadline()
    {
        // Arrange: Advance request at Finance stage
        var headId = Guid.NewGuid();
        var request = CreateSubmittedRequest(
            amount: 200_000m, deptLimit: 500_000m,
            type: BudgetRequestType.Advance, deptHeadId: headId);
        request.ApproveBy(headId);
        request.Status.Should().Be(RequestStatus.PendingFinance);

        var financeApprover = Guid.NewGuid();
        var coaId = Guid.NewGuid();

        // Act 1: Missing deadline
        var missingDeadlineResult = request.ApproveBy(financeApprover, coaId: coaId, reconciliationDeadline: null);
        missingDeadlineResult.IsFailure.Should().BeTrue();
        missingDeadlineResult.Error.Message.Should().Contain("Reconciliation deadline is required and must be in the future");

        // Act 2: Past deadline
        var pastDeadlineResult = request.ApproveBy(financeApprover, coaId: coaId, reconciliationDeadline: PastDeadline);
        pastDeadlineResult.IsFailure.Should().BeTrue();
        pastDeadlineResult.Error.Message.Should().Contain("Reconciliation deadline is required and must be in the future");

        // Act 3: Valid future deadline
        var validDeadline = DateTime.UtcNow.AddDays(14);
        var validResult = request.ApproveBy(financeApprover, coaId: coaId, reconciliationDeadline: validDeadline);
        validResult.IsSuccess.Should().BeTrue();
        request.Status.Should().Be(RequestStatus.Approved);
        request.ReconciliationDeadline.Should().Be(validDeadline);
    }

    [Fact]
    public void ApproveBy_FinanceStage_NonAdvanceRequest_WithReconciliationDeadline_ShouldFail()
    {
        // Arrange: Standard request (not an Advance)
        var headId = Guid.NewGuid();
        var request = CreateSubmittedRequest(
            type: BudgetRequestType.Standard, deptHeadId: headId);
        request.ApproveBy(headId);
        request.Status.Should().Be(RequestStatus.PendingFinance);

        // Act: Supplying deadline for standard request
        var result = request.ApproveBy(Guid.NewGuid(), coaId: Guid.NewGuid(), reconciliationDeadline: FutureDeadline);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Be("Reconciliation deadline is only valid for advance requests.");
    }

    [Fact]
    public void ApproveBy_InTerminalOrDraftStatus_ShouldFail()
    {
        // Arrange
        var request = CreateSubmittedRequest();
        request.Cancel(request.RequesterId); // Cancelled

        // Act
        var result = request.ApproveBy(Guid.NewGuid());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("Cannot approve a request in status 'Cancelled'");
    }

    // =========================================================================
    // 2. Reject Tests
    // =========================================================================

    [Fact]
    public void Reject_WithComment_InPendingStage_ShouldRejectAndFinalize()
    {
        // Arrange
        var headId = Guid.NewGuid();
        var request = CreateSubmittedRequest(deptHeadId: headId);

        // Act
        var result = request.Reject(headId, "Budget exceeded for this quarter.");

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.Status.Should().Be(RequestStatus.Rejected);
        request.FinalizedAt.Should().NotBeNull();
        request.ApprovalActions.Should().ContainSingle(a =>
            a.Stage == ApprovalStage.DeptHead
            && a.Decision == ApprovalDecision.Rejected
            && a.Comment == "Budget exceeded for this quarter.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Reject_WithoutComment_ShouldFail(string? invalidComment)
    {
        // Arrange
        var headId = Guid.NewGuid();
        var request = CreateSubmittedRequest(deptHeadId: headId);

        // Act
        var result = request.Reject(headId, invalidComment!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Be("A comment is required when rejecting.");
        request.Status.Should().Be(RequestStatus.PendingDeptHead);
    }

    // =========================================================================
    // 3. SendBack Tests
    // =========================================================================

    [Fact]
    public void SendBack_AtDeptHeadStage_ByAssignedHead_ShouldResetToSentBack()
    {
        // Arrange
        var headId = Guid.NewGuid();
        var request = CreateSubmittedRequest(deptHeadId: headId);

        // Act
        var result = request.SendBack(headId, "Please attach invoice quotation.");

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.Status.Should().Be(RequestStatus.SentBack);
        request.ApprovedAmount.Should().Be(0m);
        request.ApprovalActions.Should().ContainSingle(a =>
            a.Decision == ApprovalDecision.SentBack && a.Comment == "Please attach invoice quotation.");
    }

    [Fact]
    public void SendBack_AtDeptHeadStage_ByWrongUser_ShouldFail()
    {
        // Arrange
        var headId = Guid.NewGuid();
        var wrongUser = Guid.NewGuid();
        var request = CreateSubmittedRequest(deptHeadId: headId);

        // Act
        var result = request.SendBack(wrongUser, "Fix please.");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Be("This user is not the assigned approver for the current stage.");
    }

    [Fact]
    public void SendBack_AtApprovedStage_BeforePayment_ShouldResetToSentBack()
    {
        // Arrange: Approved request without payments
        var headId = Guid.NewGuid();
        var request = CreateSubmittedRequest(deptHeadId: headId);
        request.ApproveBy(headId);
        request.ApproveBy(Guid.NewGuid(), coaId: Guid.NewGuid());
        request.Status.Should().Be(RequestStatus.Approved);

        // Act
        var result = request.SendBack(Guid.NewGuid(), "Need price revision.");

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.Status.Should().Be(RequestStatus.SentBack);
        request.ApprovedAmount.Should().Be(0m);
    }

    [Fact]
    public void SendBack_AtApprovedStage_WithExistingPayment_ShouldFail()
    {
        // Arrange: Approved request with recorded payment
        var headId = Guid.NewGuid();
        var request = CreateSubmittedRequest(deptHeadId: headId);
        request.ApproveBy(headId);
        request.ApproveBy(Guid.NewGuid(), coaId: Guid.NewGuid());
        request.RecordPayment(100_000m, DateTime.UtcNow, Guid.NewGuid(), reference: "REF1", note: "Paid");
        request.Status.Should().Be(RequestStatus.Paid);

        // Act
        var result = request.SendBack(Guid.NewGuid(), "Try send back paid request");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("Cannot send back a request in status 'Paid'");
    }

    // =========================================================================
    // 4. ResubmitAfterSendBack Tests
    // =========================================================================

    [Fact]
    public void ResubmitAfterSendBack_WhenSentBack_ShouldRebootWorkflowAndPreserveReference()
    {
        // Arrange: Send back request
        var headId = Guid.NewGuid();
        var request = CreateSubmittedRequest(deptHeadId: headId);
        var initialReference = request.Reference;

        request.SendBack(headId, "Please clarify items.");
        request.Status.Should().Be(RequestStatus.SentBack);

        // Act: Resubmit
        var resubmitResult = request.ResubmitAfterSendBack(
            departmentId: Guid.NewGuid(),
            departmentLimit: 500_000m,
            monthlyLimit: 10_000_000m,
            monthlySpendBeforeInMmk: 0m,
            monthlyWindowStartUtc: DateTime.UtcNow,
            monthlyWindowEndUtc: DateTime.UtcNow.AddMonths(1),
            exchangeRateToMmk: 1m,
            deptHeadId: headId,
            deptHeadName: "Bob Head",
            requesterName: "Alice",
            withdrawMethodRequiresAttachment: false);

        // Assert
        resubmitResult.IsSuccess.Should().BeTrue();
        request.Status.Should().Be(RequestStatus.PendingDeptHead);
        request.Reference.Should().Be(initialReference); // Reference preserved!
    }

    [Fact]
    public void ResubmitAfterSendBack_WhenNotSentBack_ShouldFail()
    {
        // Arrange: In Draft (not SentBack)
        var request = CreateSubmittedRequest(); // Status is PendingDeptHead

        // Act
        var result = request.ResubmitAfterSendBack(
            Guid.NewGuid(), 500_000m, 10_000_000m, 0m,
            DateTime.UtcNow, DateTime.UtcNow.AddMonths(1),
            1m, Guid.NewGuid(), "Bob", "Alice", false);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Be("Only sent-back requests can be resubmitted.");
    }
}
