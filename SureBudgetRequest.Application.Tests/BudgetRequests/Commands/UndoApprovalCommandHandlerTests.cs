using FluentAssertions;
using NSubstitute;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.BudgetRequests.Commands.UndoApproval;
using SureBudgetRequest.Application.BudgetRequests.Common;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;
using Xunit;

namespace SureBudgetRequest.Application.Tests.BudgetRequests.Commands;

public class UndoApprovalCommandHandlerTests
{
    private readonly IBudgetRequestRepository _budgetRequestRepository = Substitute.For<IBudgetRequestRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly INotificationDispatcher _dispatcher = Substitute.For<INotificationDispatcher>();
    private readonly UndoApprovalCommandHandler _handler;

    public UndoApprovalCommandHandlerTests()
    {
        _handler = new UndoApprovalCommandHandler(
            _budgetRequestRepository,
            _userRepository,
            _unitOfWork,
            _dispatcher);
    }

    private static User CreateUserWithId(Guid id, UserRole role)
    {
        var user = new User("testuser", "Test User", $"test_{id:N}@example.com", Guid.NewGuid(), role);
        typeof(User).GetProperty(nameof(User.Id))!.SetValue(user, id);
        return user;
    }

    private static (BudgetRequest Request, Guid HeadId) CreatePendingManagementRequest()
    {
        var requesterId = Guid.NewGuid();
        var headId = Guid.NewGuid();

        var request = BudgetRequest.CreateDraft(
            requesterId, "Requester", BudgetRequestType.Standard, headId, "Bob Head",
            DateTime.UtcNow.AddDays(5),
            800_000m, "MMK", "Test", "Requester", "Staff", Guid.NewGuid(),
            false, null);

        request.Submit(
            Guid.NewGuid(), 500_000m, 10_000_000m, 0m,
            DateTime.UtcNow, DateTime.UtcNow.AddMonths(1),
            1m, headId, "Bob Head", "Requester", false, 1);

        request.ApproveBy(headId);
        return (request, headId);
    }

    [Fact]
    public async Task Handle_WhenRequestNotFound_ReturnsNotFoundError()
    {
        var reqId = Guid.NewGuid();
        _budgetRequestRepository.GetByIdAsync(reqId, Arg.Any<CancellationToken>()).Returns((BudgetRequest?)null);

        var result = await _handler.Handle(new UndoApprovalCommand(reqId, Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BudgetRequestErrors.NotFound(reqId));
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenUserNotFound_ReturnsNotFoundError()
    {
        var (request, _) = CreatePendingManagementRequest();
        var userId = Guid.NewGuid();

        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns((User?)null);

        var result = await _handler.Handle(new UndoApprovalCommand(request.Id, userId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.NotFound(userId));
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(UserRole.Employee)]
    [InlineData(UserRole.Management)]
    [InlineData(UserRole.Finance)]
    [InlineData(UserRole.Accounting)]
    [InlineData(UserRole.Support)]
    public async Task Handle_WhenUserIsNotAdmin_ReturnsRoleUnauthorizedError(UserRole role)
    {
        var (request, headId) = CreatePendingManagementRequest();
        var nonAdmin = CreateUserWithId(headId, role);

        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(nonAdmin.Id, Arg.Any<CancellationToken>()).Returns(nonAdmin);

        var result = await _handler.Handle(new UndoApprovalCommand(request.Id, nonAdmin.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("User.RoleUnauthorized");
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenUserIsAdmin_UndoesApprovalAndDispatchesNotification()
    {
        var (request, _) = CreatePendingManagementRequest();
        var admin = CreateUserWithId(Guid.NewGuid(), UserRole.Admin);

        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(admin.Id, Arg.Any<CancellationToken>()).Returns(admin);

        var result = await _handler.Handle(new UndoApprovalCommand(request.Id, admin.Id, "Admin correction"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        request.Status.Should().Be(RequestStatus.PendingDeptHead);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _dispatcher.Received(1).DispatchApprovalUndoneAsync(
            request, "Department Head", admin.FullName, "Admin correction", Arg.Any<CancellationToken>());
    }
}
