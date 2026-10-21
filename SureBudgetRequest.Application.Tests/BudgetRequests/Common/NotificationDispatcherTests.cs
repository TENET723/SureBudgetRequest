using FluentAssertions;
using NSubstitute;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.Abstractions.Services;
using SureBudgetRequest.Application.BudgetRequests.Common;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;
using Xunit;

namespace SureBudgetRequest.Application.Tests.BudgetRequests.Common;

public class NotificationDispatcherTests
{
    private readonly IUserRepository _userRepository;
    private readonly INotificationService _notificationService;
    private readonly NotificationDispatcher _sut;

    public NotificationDispatcherTests()
    {
        _userRepository = Substitute.For<IUserRepository>();
        _notificationService = Substitute.For<INotificationService>();
        _sut = new NotificationDispatcher(_userRepository, _notificationService);
    }

    // =========================================================================
    // Test Helpers
    // =========================================================================

    private static User CreateUserWithId(Guid id, UserRole role, bool isFinanceApprover = false)
    {
        var user = new User("testuser", "Test User", $"test_{id:N}@example.com", Guid.NewGuid(), role);
        if (role == UserRole.Finance && isFinanceApprover)
        {
            user.SetFinanceApprover(true);
        }
        typeof(User).GetProperty(nameof(User.Id))!.SetValue(user, id);
        return user;
    }

    private static BudgetRequest CreateRequest(
        Guid requestId,
        Guid requesterId,
        Guid deptHeadId,
        BudgetRequestType type = BudgetRequestType.Standard,
        decimal amount = 100_000m,
        string? monthlyJustification = null)
    {
        var request = BudgetRequest.CreateDraft(
            requesterId,
            "Requester Alice",
            type,
            deptHeadId,
            "Dept Head Bob",
            DateTime.UtcNow.AddDays(2),
            amount,
            "MMK",
            "Business Trip Expenses",
            "Alice Withdrawer",
            "Staff Engineer",
            Guid.NewGuid(),
            false,
            null,
            monthlyJustification);

        typeof(BudgetRequest).GetProperty(nameof(BudgetRequest.Id))!.SetValue(request, requestId);
        return request;
    }

    private static void SetStatus(BudgetRequest request, RequestStatus status)
    {
        typeof(BudgetRequest).GetProperty(nameof(BudgetRequest.Status))!.SetValue(request, status);
    }

    // =========================================================================
    // 1. DispatchAsync - Submission Transitions
    // =========================================================================

    [Theory]
    [InlineData(RequestStatus.Draft)]
    [InlineData(RequestStatus.SentBack)]
    public async Task DispatchAsync_StandardSubmissionToDeptHead_ShouldNotifyDeptHead(RequestStatus previousStatus)
    {
        // Arrange
        var reqId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var deptHeadId = Guid.NewGuid();
        var request = CreateRequest(reqId, requesterId, deptHeadId);
        request.Submit(
            Guid.NewGuid(), 100_000m, 10_000_000m, 0m,
            DateTime.UtcNow, DateTime.UtcNow.AddMonths(1),
            1m, deptHeadId, "Dept Head Bob", "Requester Alice", false, 1);

        request.Status.Should().Be(RequestStatus.PendingDeptHead);

        // Act
        await _sut.DispatchAsync(request, previousStatus, requesterId, "Requester Alice", null, CancellationToken.None);

        // Assert
        await _notificationService.Received(1).SendAsync(
            Arg.Is<NotificationEvent>(e =>
                e.Trigger == NotificationTrigger.SubmittedToDeptHead &&
                e.BudgetRequestId == reqId &&
                e.RecipientUserIds.SequenceEqual(new[] { deptHeadId }) &&
                e.RequesterName == "Requester Alice"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchAsync_DeptHeadSelfSubmitted_UnderLimit_ShouldNotifyFinanceApprovers()
    {
        // Arrange: dept head is requester
        var reqId = Guid.NewGuid();
        var deptHeadId = Guid.NewGuid();
        var request = CreateRequest(reqId, deptHeadId, deptHeadId);

        var financeApprover1 = CreateUserWithId(Guid.NewGuid(), UserRole.Finance, isFinanceApprover: true);
        var financeApprover2 = CreateUserWithId(Guid.NewGuid(), UserRole.Finance, isFinanceApprover: true);
        _userRepository.ListAsync(
            role: UserRole.Finance,
            isFinanceApprover: true,
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new[] { financeApprover1, financeApprover2 });

        request.Submit(
            Guid.NewGuid(), 100_000m, 10_000_000m, 0m,
            DateTime.UtcNow, DateTime.UtcNow.AddMonths(1),
            1m, deptHeadId, "Dept Head Bob", "Requester Alice", false, 1);

        request.Status.Should().Be(RequestStatus.PendingFinance);

        // Act
        await _sut.DispatchAsync(request, RequestStatus.Draft, deptHeadId, "Dept Head Bob", null, CancellationToken.None);

        // Assert
        await _notificationService.Received(1).SendAsync(
            Arg.Is<NotificationEvent>(e =>
                e.Trigger == NotificationTrigger.SubmittedToFinance &&
                e.BudgetRequestId == reqId &&
                e.RecipientUserIds.SequenceEqual(new[] { financeApprover1.Id, financeApprover2.Id })),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchAsync_DeptHeadSelfSubmitted_OverLimit_ShouldNotifyManagement()
    {
        // Arrange: dept head is requester, amount exceeds limit with justification
        var reqId = Guid.NewGuid();
        var deptHeadId = Guid.NewGuid();
        var request = CreateRequest(reqId, deptHeadId, deptHeadId, amount: 15_000_000m, monthlyJustification: "Special requirement");

        var mgmtUser1 = CreateUserWithId(Guid.NewGuid(), UserRole.Management);
        var mgmtUser2 = CreateUserWithId(Guid.NewGuid(), UserRole.Management);
        _userRepository.ListAsync(
            role: UserRole.Management,
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new[] { mgmtUser1, mgmtUser2 });

        request.Submit(
            Guid.NewGuid(), 10_000_000m, 20_000_000m, 0m,
            DateTime.UtcNow, DateTime.UtcNow.AddMonths(1),
            1m, deptHeadId, "Dept Head Bob", "Requester Alice", false, 1);

        request.Status.Should().Be(RequestStatus.PendingManagement);

        // Act
        await _sut.DispatchAsync(request, RequestStatus.Draft, deptHeadId, "Dept Head Bob", null, CancellationToken.None);

        // Assert
        await _notificationService.Received(1).SendAsync(
            Arg.Is<NotificationEvent>(e =>
                e.Trigger == NotificationTrigger.SubmittedToManagement &&
                e.BudgetRequestId == reqId &&
                e.RecipientUserIds.SequenceEqual(new[] { mgmtUser1.Id, mgmtUser2.Id })),
            Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // 2. DispatchAsync - Dept Head Decisions
    // =========================================================================

    [Fact]
    public async Task DispatchAsync_DeptHeadApprovedToFinance_ShouldNotifyFinanceApprovers()
    {
        // Arrange
        var reqId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var deptHeadId = Guid.NewGuid();
        var request = CreateRequest(reqId, requesterId, deptHeadId);
        request.Submit(
            Guid.NewGuid(), 100_000m, 10_000_000m, 0m,
            DateTime.UtcNow, DateTime.UtcNow.AddMonths(1),
            1m, deptHeadId, "Dept Head Bob", "Alice", false, 1);

        var financeApprover = CreateUserWithId(Guid.NewGuid(), UserRole.Finance, isFinanceApprover: true);
        _userRepository.ListAsync(
            role: UserRole.Finance,
            isFinanceApprover: true,
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new[] { financeApprover });

        // Dept Head approves (under limit routes to PendingFinance)
        request.ApproveBy(deptHeadId);
        request.Status.Should().Be(RequestStatus.PendingFinance);

        // Act
        await _sut.DispatchAsync(request, RequestStatus.PendingDeptHead, deptHeadId, "Dept Head Bob", "Approved by Dept Head", CancellationToken.None);

        // Assert
        await _notificationService.Received(1).SendAsync(
            Arg.Is<NotificationEvent>(e =>
                e.Trigger == NotificationTrigger.DeptHeadApprovedToFinance &&
                e.BudgetRequestId == reqId &&
                e.RecipientUserIds.SequenceEqual(new[] { financeApprover.Id }) &&
                e.Comment == "Approved by Dept Head"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchAsync_DeptHeadApprovedToManagement_ShouldNotifyManagement()
    {
        // Arrange: Over limit request routes to PendingManagement upon Dept Head approval
        var reqId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var deptHeadId = Guid.NewGuid();
        var request = CreateRequest(reqId, requesterId, deptHeadId, amount: 15_000_000m, monthlyJustification: "Approved over budget");

        request.Submit(
            Guid.NewGuid(), 10_000_000m, 20_000_000m, 0m,
            DateTime.UtcNow, DateTime.UtcNow.AddMonths(1),
            1m, deptHeadId, "Dept Head Bob", "Alice", false, 1);

        var mgmtUser = CreateUserWithId(Guid.NewGuid(), UserRole.Management);
        _userRepository.ListAsync(
            role: UserRole.Management,
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new[] { mgmtUser });

        request.ApproveBy(deptHeadId);
        request.Status.Should().Be(RequestStatus.PendingManagement);

        // Act
        await _sut.DispatchAsync(request, RequestStatus.PendingDeptHead, deptHeadId, "Dept Head Bob", null, CancellationToken.None);

        // Assert
        await _notificationService.Received(1).SendAsync(
            Arg.Is<NotificationEvent>(e =>
                e.Trigger == NotificationTrigger.DeptHeadApprovedToManagement &&
                e.BudgetRequestId == reqId &&
                e.RecipientUserIds.SequenceEqual(new[] { mgmtUser.Id })),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchAsync_DeptHeadRejected_ShouldNotifyRequester()
    {
        // Arrange
        var reqId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var deptHeadId = Guid.NewGuid();
        var request = CreateRequest(reqId, requesterId, deptHeadId);
        request.Submit(
            Guid.NewGuid(), 100_000m, 10_000_000m, 0m,
            DateTime.UtcNow, DateTime.UtcNow.AddMonths(1),
            1m, deptHeadId, "Dept Head Bob", "Alice", false, 1);

        request.Reject(deptHeadId, "Not in quarterly scope");
        request.Status.Should().Be(RequestStatus.Rejected);

        // Act
        await _sut.DispatchAsync(request, RequestStatus.PendingDeptHead, deptHeadId, "Dept Head Bob", "Not in quarterly scope", CancellationToken.None);

        // Assert
        await _notificationService.Received(1).SendAsync(
            Arg.Is<NotificationEvent>(e =>
                e.Trigger == NotificationTrigger.DeptHeadRejectedToRequester &&
                e.BudgetRequestId == reqId &&
                e.RecipientUserIds.SequenceEqual(new[] { requesterId }) &&
                e.Comment == "Not in quarterly scope"),
            Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // 3. DispatchAsync - Management Decisions
    // =========================================================================

    [Fact]
    public async Task DispatchAsync_ManagementApproved_ShouldNotifyFinanceApprovers()
    {
        // Arrange
        var reqId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var deptHeadId = Guid.NewGuid();
        var mgmtId = Guid.NewGuid();
        var request = CreateRequest(reqId, requesterId, deptHeadId, amount: 15_000_000m, monthlyJustification: "Approved over budget");

        request.Submit(
            Guid.NewGuid(), 10_000_000m, 20_000_000m, 0m,
            DateTime.UtcNow, DateTime.UtcNow.AddMonths(1),
            1m, deptHeadId, "Dept Head Bob", "Alice", false, 1);

        request.ApproveBy(deptHeadId); // Moves to PendingManagement
        request.ApproveBy(mgmtId);     // Moves to PendingFinance
        request.Status.Should().Be(RequestStatus.PendingFinance);

        var financeApprover = CreateUserWithId(Guid.NewGuid(), UserRole.Finance, isFinanceApprover: true);
        _userRepository.ListAsync(
            role: UserRole.Finance,
            isFinanceApprover: true,
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new[] { financeApprover });

        // Act
        await _sut.DispatchAsync(request, RequestStatus.PendingManagement, mgmtId, "CEO Carol", null, CancellationToken.None);

        // Assert
        await _notificationService.Received(1).SendAsync(
            Arg.Is<NotificationEvent>(e =>
                e.Trigger == NotificationTrigger.ManagementApprovedToFinance &&
                e.BudgetRequestId == reqId &&
                e.RecipientUserIds.SequenceEqual(new[] { financeApprover.Id })),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchAsync_ManagementRejected_ShouldNotifyRequester()
    {
        // Arrange
        var reqId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var deptHeadId = Guid.NewGuid();
        var mgmtId = Guid.NewGuid();
        var request = CreateRequest(reqId, requesterId, deptHeadId, amount: 15_000_000m, monthlyJustification: "Approved over budget");

        request.Submit(
            Guid.NewGuid(), 10_000_000m, 20_000_000m, 0m,
            DateTime.UtcNow, DateTime.UtcNow.AddMonths(1),
            1m, deptHeadId, "Dept Head Bob", "Alice", false, 1);

        request.ApproveBy(deptHeadId);
        request.Reject(mgmtId, "Budget exceeded for Q3");
        request.Status.Should().Be(RequestStatus.Rejected);

        // Act
        await _sut.DispatchAsync(request, RequestStatus.PendingManagement, mgmtId, "CEO Carol", "Budget exceeded for Q3", CancellationToken.None);

        // Assert
        await _notificationService.Received(1).SendAsync(
            Arg.Is<NotificationEvent>(e =>
                e.Trigger == NotificationTrigger.ManagementRejectedToRequester &&
                e.BudgetRequestId == reqId &&
                e.RecipientUserIds.SequenceEqual(new[] { requesterId }) &&
                e.Comment == "Budget exceeded for Q3"),
            Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // 4. DispatchAsync - Finance Decisions & Payments
    // =========================================================================

    [Fact]
    public async Task DispatchAsync_FinanceApproved_StandardRequest_ShouldNotifyRequester()
    {
        // Arrange
        var reqId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var deptHeadId = Guid.NewGuid();
        var financeId = Guid.NewGuid();
        var request = CreateRequest(reqId, requesterId, deptHeadId, BudgetRequestType.Standard);
        request.Submit(
            Guid.NewGuid(), 100_000m, 10_000_000m, 0m,
            DateTime.UtcNow, DateTime.UtcNow.AddMonths(1),
            1m, deptHeadId, "Dept Head Bob", "Alice", false, 1);

        request.ApproveBy(deptHeadId);
        request.ApproveBy(financeId, coaId: Guid.NewGuid());
        request.Status.Should().Be(RequestStatus.Approved);

        // Act
        await _sut.DispatchAsync(request, RequestStatus.PendingFinance, financeId, "Finance Dave", null, CancellationToken.None);

        // Assert
        await _notificationService.Received(1).SendAsync(
            Arg.Is<NotificationEvent>(e =>
                e.Trigger == NotificationTrigger.FinanceApprovedToRequester &&
                e.BudgetRequestId == reqId &&
                e.RecipientUserIds.SequenceEqual(new[] { requesterId })),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchAsync_FinanceApproved_AdvanceRequest_ShouldNotifyRequesterWithAdvanceTrigger()
    {
        // Arrange
        var reqId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var deptHeadId = Guid.NewGuid();
        var financeId = Guid.NewGuid();
        var request = CreateRequest(reqId, requesterId, deptHeadId, BudgetRequestType.Advance);
        request.Submit(
            Guid.NewGuid(), 100_000m, 10_000_000m, 0m,
            DateTime.UtcNow, DateTime.UtcNow.AddMonths(1),
            1m, deptHeadId, "Dept Head Bob", "Alice", false, 1);

        request.ApproveBy(deptHeadId);
        request.ApproveBy(financeId, coaId: Guid.NewGuid(), reconciliationDeadline: DateTime.UtcNow.AddDays(14));
        request.Status.Should().Be(RequestStatus.Approved);

        // Act
        await _sut.DispatchAsync(request, RequestStatus.PendingFinance, financeId, "Finance Dave", null, CancellationToken.None);

        // Assert
        await _notificationService.Received(1).SendAsync(
            Arg.Is<NotificationEvent>(e =>
                e.Trigger == NotificationTrigger.AdvanceApprovedToRequester &&
                e.BudgetRequestId == reqId &&
                e.RecipientUserIds.SequenceEqual(new[] { requesterId })),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(RequestStatus.Approved)]
    [InlineData(RequestStatus.PartiallyPaid)]
    public async Task DispatchAsync_PaymentRecorded_ShouldNotifyRequester(RequestStatus previousStatus)
    {
        // Arrange
        var reqId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var deptHeadId = Guid.NewGuid();
        var financeId = Guid.NewGuid();
        var request = CreateRequest(reqId, requesterId, deptHeadId);
        request.Submit(
            Guid.NewGuid(), 100_000m, 10_000_000m, 0m,
            DateTime.UtcNow, DateTime.UtcNow.AddMonths(1),
            1m, deptHeadId, "Dept Head Bob", "Alice", false, 1);

        request.ApproveBy(deptHeadId);
        request.ApproveBy(financeId, coaId: Guid.NewGuid());
        request.RecordPayment(100_000m, DateTime.UtcNow, financeId, "TRX-12345", "Full Payment");
        request.Status.Should().Be(RequestStatus.Paid);

        // Act
        await _sut.DispatchAsync(request, previousStatus, financeId, "Finance Dave", null, CancellationToken.None);

        // Assert
        await _notificationService.Received(1).SendAsync(
            Arg.Is<NotificationEvent>(e =>
                e.Trigger == NotificationTrigger.FinancePaidToRequester &&
                e.BudgetRequestId == reqId &&
                e.RecipientUserIds.SequenceEqual(new[] { requesterId })),
            Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // 5. DispatchAsync - Sent Back Transitions
    // =========================================================================

    [Theory]
    [InlineData(RequestStatus.PendingDeptHead)]
    [InlineData(RequestStatus.PendingManagement)]
    [InlineData(RequestStatus.PendingFinance)]
    [InlineData(RequestStatus.Approved)]
    public async Task DispatchAsync_SentBackFromAnyStage_ShouldNotifyRequester(RequestStatus previousStatus)
    {
        // Arrange
        var reqId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var deptHeadId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var request = CreateRequest(reqId, requesterId, deptHeadId);

        SetStatus(request, RequestStatus.SentBack);

        // Act
        await _sut.DispatchAsync(request, previousStatus, actorId, "Approver", "Please fix attachments", CancellationToken.None);

        // Assert
        await _notificationService.Received(1).SendAsync(
            Arg.Is<NotificationEvent>(e =>
                e.Trigger == NotificationTrigger.SentBackToRequester &&
                e.BudgetRequestId == reqId &&
                e.RecipientUserIds.SequenceEqual(new[] { requesterId }) &&
                e.Comment == "Please fix attachments"),
            Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // 6. DispatchAsync - Unhandled / Non-Notifiable Transitions
    // =========================================================================

    [Fact]
    public async Task DispatchAsync_UnhandledStatusChange_ShouldNotDispatchAnyNotification()
    {
        // Arrange
        var reqId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var deptHeadId = Guid.NewGuid();
        var request = CreateRequest(reqId, requesterId, deptHeadId);

        // Draft to Cancelled
        SetStatus(request, RequestStatus.Cancelled);

        // Act
        await _sut.DispatchAsync(request, RequestStatus.Draft, requesterId, "Alice", null, CancellationToken.None);

        // Assert
        await _notificationService.DidNotReceive().SendAsync(
            Arg.Any<NotificationEvent>(), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // 7. Reconciliation Dispatches
    // =========================================================================

    [Fact]
    public async Task DispatchReconciliationSubmittedAsync_ExactMatch_ShouldNotifyFinanceOnly()
    {
        // Arrange
        var reqId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var deptHeadId = Guid.NewGuid();
        var request = CreateRequest(reqId, requesterId, deptHeadId, BudgetRequestType.Advance);
        SetStatus(request, RequestStatus.Reconciled);

        var financeApprover = CreateUserWithId(Guid.NewGuid(), UserRole.Finance, isFinanceApprover: true);
        _userRepository.ListAsync(
            role: UserRole.Finance,
            isFinanceApprover: true,
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new[] { financeApprover });

        // Act
        await _sut.DispatchReconciliationSubmittedAsync(request, "Requester Alice", CancellationToken.None);

        // Assert
        await _notificationService.Received(1).SendAsync(
            Arg.Is<NotificationEvent>(e =>
                e.Trigger == NotificationTrigger.ReconciliationSubmittedToFinance &&
                e.BudgetRequestId == reqId &&
                e.RecipientUserIds.SequenceEqual(new[] { financeApprover.Id })),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchReconciliationSubmittedAsync_AwaitingRefund_ShouldNotifyFinanceAndRequester()
    {
        // Arrange
        var reqId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var deptHeadId = Guid.NewGuid();
        var request = CreateRequest(reqId, requesterId, deptHeadId, BudgetRequestType.Advance);
        SetStatus(request, RequestStatus.AwaitingRefund);

        var financeApprover = CreateUserWithId(Guid.NewGuid(), UserRole.Finance, isFinanceApprover: true);
        _userRepository.ListAsync(
            role: UserRole.Finance,
            isFinanceApprover: true,
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new[] { financeApprover });

        // Act
        await _sut.DispatchReconciliationSubmittedAsync(request, "Requester Alice", CancellationToken.None);

        // Assert: 2 events sent
        await _notificationService.Received(1).SendAsync(
            Arg.Is<NotificationEvent>(e =>
                e.Trigger == NotificationTrigger.ReconciliationSubmittedToFinance &&
                e.BudgetRequestId == reqId &&
                e.RecipientUserIds.SequenceEqual(new[] { financeApprover.Id })),
            Arg.Any<CancellationToken>());

        await _notificationService.Received(1).SendAsync(
            Arg.Is<NotificationEvent>(e =>
                e.Trigger == NotificationTrigger.AdvanceAwaitingRefund &&
                e.BudgetRequestId == reqId &&
                e.RecipientUserIds.Contains(financeApprover.Id) &&
                e.RecipientUserIds.Contains(requesterId)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchReconciliationSubmittedAsync_AwaitingReimbursement_ShouldNotifyFinanceAndRequester()
    {
        // Arrange
        var reqId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var deptHeadId = Guid.NewGuid();
        var request = CreateRequest(reqId, requesterId, deptHeadId, BudgetRequestType.Advance);
        SetStatus(request, RequestStatus.AwaitingReimbursement);

        var financeApprover = CreateUserWithId(Guid.NewGuid(), UserRole.Finance, isFinanceApprover: true);
        _userRepository.ListAsync(
            role: UserRole.Finance,
            isFinanceApprover: true,
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new[] { financeApprover });

        // Act
        await _sut.DispatchReconciliationSubmittedAsync(request, "Requester Alice", CancellationToken.None);

        // Assert: 2 events sent
        await _notificationService.Received(1).SendAsync(
            Arg.Is<NotificationEvent>(e =>
                e.Trigger == NotificationTrigger.ReconciliationSubmittedToFinance &&
                e.BudgetRequestId == reqId &&
                e.RecipientUserIds.SequenceEqual(new[] { financeApprover.Id })),
            Arg.Any<CancellationToken>());

        await _notificationService.Received(1).SendAsync(
            Arg.Is<NotificationEvent>(e =>
                e.Trigger == NotificationTrigger.AdvanceAwaitingReimbursement &&
                e.BudgetRequestId == reqId &&
                e.RecipientUserIds.Contains(financeApprover.Id) &&
                e.RecipientUserIds.Contains(requesterId)),
            Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // 8. Settlement Dispatches
    // =========================================================================

    [Fact]
    public async Task DispatchRefundRecordedAsync_ShouldNotifyRequester()
    {
        // Arrange
        var reqId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var deptHeadId = Guid.NewGuid();
        var request = CreateRequest(reqId, requesterId, deptHeadId, BudgetRequestType.Advance);

        // Act
        await _sut.DispatchRefundRecordedAsync(request, "Finance Dave", CancellationToken.None);

        // Assert
        await _notificationService.Received(1).SendAsync(
            Arg.Is<NotificationEvent>(e =>
                e.Trigger == NotificationTrigger.RefundRecordedToRequester &&
                e.BudgetRequestId == reqId &&
                e.RecipientUserIds.SequenceEqual(new[] { requesterId }) &&
                e.ActorName == "Finance Dave"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchReimbursementRecordedAsync_ShouldNotifyRequester()
    {
        // Arrange
        var reqId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var deptHeadId = Guid.NewGuid();
        var request = CreateRequest(reqId, requesterId, deptHeadId, BudgetRequestType.Advance);

        // Act
        await _sut.DispatchReimbursementRecordedAsync(request, "Finance Dave", CancellationToken.None);

        // Assert
        await _notificationService.Received(1).SendAsync(
            Arg.Is<NotificationEvent>(e =>
                e.Trigger == NotificationTrigger.ReimbursementRecordedToRequester &&
                e.BudgetRequestId == reqId &&
                e.RecipientUserIds.SequenceEqual(new[] { requesterId }) &&
                e.ActorName == "Finance Dave"),
            Arg.Any<CancellationToken>());
    }
}
