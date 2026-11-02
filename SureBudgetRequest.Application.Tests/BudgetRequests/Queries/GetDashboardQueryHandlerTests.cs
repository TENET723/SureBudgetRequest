using FluentAssertions;
using NSubstitute;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.BudgetRequests.Queries.GetDashboard;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;
using Xunit;

namespace SureBudgetRequest.Application.Tests.BudgetRequests.Queries;

public class GetDashboardQueryHandlerTests
{
    private readonly IBudgetRequestRepository _requestRepository = Substitute.For<IBudgetRequestRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();

    private readonly GetDashboardQueryHandler _handler;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _mgmtDeptId = Guid.NewGuid();

    public GetDashboardQueryHandlerTests()
    {
        _currentUser.IsLoaded.Returns(true);
        _currentUser.UserId.Returns(_userId);
        _currentUser.DepartmentId.Returns(_mgmtDeptId);
        _currentUser.Role.Returns(UserRole.Management);
        _currentUser.IsDepartmentHead.Returns(true);

        _handler = new GetDashboardQueryHandler(
            _requestRepository,
            _userRepository,
            _currentUser);
    }

    private static BudgetRequest CreateSubmittedRequest(Guid requesterId, Guid deptHeadId)
    {
        var request = BudgetRequest.CreateDraft(
            requesterId,
            "Requester",
            BudgetRequestType.Standard,
            deptHeadId,
            "Head",
            DateTime.UtcNow.AddDays(1),
            100_000m,
            "MMK",
            "Hardware",
            "Withdrawer",
            "Title",
            Guid.NewGuid(),
            false,
            null);
        typeof(BudgetRequest).GetProperty(nameof(BudgetRequest.Id))!.SetValue(request, Guid.NewGuid());
        //request.Submit(deptHeadId);
        request.Submit(
            Guid.NewGuid(), 1_000_000m, 10_000_000m, 0m,
            DateTime.UtcNow, DateTime.UtcNow.AddMonths(1),
            1m, deptHeadId, "Bob Head", "Alice", false, 1);
        return request;
    }

    [Fact]
    public async Task Handle_WhenManagementUserIsDeptHead_IncludesBothPendingDeptHeadAndPendingManagementInApprovalQueue()
    {
        // Arrange
        var employeeRequesterId = Guid.NewGuid();
        var deptPendingRequest = CreateSubmittedRequest(employeeRequesterId, _userId);

        var mgmtRequesterId = Guid.NewGuid();
        var mgmtPendingRequest = CreateSubmittedRequest(mgmtRequesterId, Guid.NewGuid());
        // Move to PendingManagement
        mgmtPendingRequest.ApproveBy(_userId);

        _requestRepository.ListAsync(
            status: RequestStatus.PendingDeptHead,
            deptHeadId: _userId,
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new List<BudgetRequest> { deptPendingRequest });

        _requestRepository.ListAsync(
            status: RequestStatus.PendingManagement,
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new List<BudgetRequest> { mgmtPendingRequest });

        _userRepository.ListAsync(includeInactive: true, cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new List<User>());

        // Act
        var result = await _handler.Handle(new GetDashboardQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.MyApprovalQueue.Should().NotBeNull();
        result.Value.MyApprovalQueue!.TotalCount.Should().Be(2);
        result.Value.MyApprovalQueue.TopItems.Select(i => i.Id)
            .Should().Contain(new[] { deptPendingRequest.Id, mgmtPendingRequest.Id });
    }

    [Fact]
    public async Task Handle_WhenRequesterIsCurrentUser_ExcludesOwnRequestsFromApprovalQueue()
    {
        // Arrange: deptPendingRequest submitted by the user themselves
        var ownDeptPendingRequest = CreateSubmittedRequest(_userId, _userId);

        _requestRepository.ListAsync(
            status: RequestStatus.PendingDeptHead,
            deptHeadId: _userId,
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new List<BudgetRequest> { ownDeptPendingRequest });

        _requestRepository.ListAsync(
            status: RequestStatus.PendingManagement,
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new List<BudgetRequest>());

        _userRepository.ListAsync(includeInactive: true, cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new List<User>());

        // Act
        var result = await _handler.Handle(new GetDashboardQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.MyApprovalQueue.Should().NotBeNull();
        result.Value.MyApprovalQueue!.TotalCount.Should().Be(0);
        result.Value.MyApprovalQueue.TopItems.Should().BeEmpty();
    }
}
