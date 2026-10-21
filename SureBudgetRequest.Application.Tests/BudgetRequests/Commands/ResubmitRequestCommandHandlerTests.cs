using FluentAssertions;
using NSubstitute;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.Abstractions.Services;
using SureBudgetRequest.Application.BudgetRequests.Commands.ResubmitRequest;
using SureBudgetRequest.Application.BudgetRequests.Common;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;
using Xunit;

namespace SureBudgetRequest.Application.Tests.BudgetRequests.Commands;

public class ResubmitRequestCommandHandlerTests
{
    private readonly IBudgetRequestRepository _budgetRequestRepository = Substitute.For<IBudgetRequestRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IDepartmentRepository _departmentRepository = Substitute.For<IDepartmentRepository>();
    private readonly ICurrencyRepository _currencyRepository = Substitute.For<ICurrencyRepository>();
    private readonly IWithdrawMethodRepository _withdrawMethodRepository = Substitute.For<IWithdrawMethodRepository>();
    private readonly IDepartmentMonthlyBudgetRepository _monthlyBudgetRepository = Substitute.For<IDepartmentMonthlyBudgetRepository>();
    private readonly IAppSettingRepository _appSettingRepository = Substitute.For<IAppSettingRepository>();
    private readonly IDateTimeProvider _clock = Substitute.For<IDateTimeProvider>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly INotificationDispatcher _dispatcher = Substitute.For<INotificationDispatcher>();
    private readonly ResubmitRequestCommandHandler _handler;

    public ResubmitRequestCommandHandlerTests()
    {
        _clock.BusinessNow.Returns(new DateTime(2026, 5, 15, 12, 0, 0, DateTimeKind.Utc));
        _clock.BusinessUtcOffset.Returns(TimeSpan.FromHours(6.5));

        _handler = new ResubmitRequestCommandHandler(
            _budgetRequestRepository,
            _userRepository,
            _departmentRepository,
            _currencyRepository,
            _withdrawMethodRepository,
            _monthlyBudgetRepository,
            _appSettingRepository,
            _clock,
            _unitOfWork,
            _dispatcher);
    }

    private static BudgetRequest CreateSentBackRequest(Guid requesterId, Guid deptHeadId, Guid withdrawMethodId)
    {
        var request = BudgetRequest.CreateDraft(
            requesterId, "Alice", BudgetRequestType.Standard, deptHeadId, "Bob Head",
            DateTime.UtcNow.AddHours(6.5).Date.AddDays(2),
            50_000m, "MMK", "Test", "Alice", "Job", withdrawMethodId,
            false, null);

        request.Submit(
            Guid.NewGuid(), 1_000_000m, 10_000_000m, 0m,
            DateTime.UtcNow, DateTime.UtcNow.AddMonths(1),
            1m, deptHeadId, "Bob Head", "Alice", false, 1);

        request.SendBack(deptHeadId, "Please adjust budget details");

        return request;
    }

    [Fact]
    public async Task Handle_WhenBudgetRequestNotFound_ReturnsNotFoundError()
    {
        // Arrange
        var reqId = Guid.NewGuid();
        _budgetRequestRepository.GetByIdAsync(reqId, Arg.Any<CancellationToken>()).Returns((BudgetRequest?)null);

        var command = new ResubmitRequestCommand(reqId, Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BudgetRequestErrors.NotFound(reqId));

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenUserIsNotRequester_ReturnsOnlyRequesterError()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var request = CreateSentBackRequest(requesterId, Guid.NewGuid(), Guid.NewGuid());

        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);

        var command = new ResubmitRequestCommand(request.Id, otherUserId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BudgetRequestErrors.OnlyRequesterCanResubmit);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenRequesterNotFound_ReturnsUserNotFoundError()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var request = CreateSentBackRequest(requesterId, Guid.NewGuid(), Guid.NewGuid());

        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(requesterId, Arg.Any<CancellationToken>()).Returns((User?)null);

        var command = new ResubmitRequestCommand(request.Id, requesterId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.NotFound(requesterId));

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenDepartmentNotFound_ReturnsDepartmentNotFoundError()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var deptId = Guid.NewGuid();
        var requester = new User("alice", "Alice", "alice@sure.com", deptId, UserRole.Employee);
        var request = CreateSentBackRequest(requesterId, Guid.NewGuid(), Guid.NewGuid());

        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(requesterId, Arg.Any<CancellationToken>()).Returns(requester);
        _departmentRepository.GetByIdAsync(deptId, Arg.Any<CancellationToken>()).Returns((Department?)null);

        var command = new ResubmitRequestCommand(request.Id, requesterId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BudgetRequestErrors.DepartmentNotFound);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenDepartmentHeadNotAssigned_ReturnsDepartmentHeadNotAssignedError()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var dept = new Department("IT", null, 1_000_000m, 10_000_000m);
        var requester = new User("alice", "Alice", "alice@sure.com", dept.Id, UserRole.Employee);
        var request = CreateSentBackRequest(requesterId, Guid.NewGuid(), Guid.NewGuid());

        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(requesterId, Arg.Any<CancellationToken>()).Returns(requester);
        _departmentRepository.GetByIdAsync(dept.Id, Arg.Any<CancellationToken>()).Returns(dept);

        var command = new ResubmitRequestCommand(request.Id, requesterId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BudgetRequestErrors.DepartmentHeadNotAssigned);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenDepartmentHeadNotFound_ReturnsDepartmentHeadNotFoundError()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var headId = Guid.NewGuid();
        var dept = new Department("IT", headId, 1_000_000m, 10_000_000m);
        var requester = new User("alice", "Alice", "alice@sure.com", dept.Id, UserRole.Employee);
        var request = CreateSentBackRequest(requesterId, headId, Guid.NewGuid());

        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(requesterId, Arg.Any<CancellationToken>()).Returns(requester);
        _departmentRepository.GetByIdAsync(dept.Id, Arg.Any<CancellationToken>()).Returns(dept);
        _userRepository.GetByIdAsync(headId, Arg.Any<CancellationToken>()).Returns((User?)null);

        var command = new ResubmitRequestCommand(request.Id, requesterId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BudgetRequestErrors.DepartmentHeadNotFound);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenCurrencyNotFound_ReturnsCurrencyNotFoundError()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var headId = Guid.NewGuid();
        var dept = new Department("IT", headId, 1_000_000m, 10_000_000m);
        var requester = new User("alice", "Alice", "alice@sure.com", dept.Id, UserRole.Employee);
        var head = new User("head", "Dept Head", "head@sure.com", dept.Id, UserRole.DepartmentHead);
        var request = CreateSentBackRequest(requesterId, headId, Guid.NewGuid());

        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(requesterId, Arg.Any<CancellationToken>()).Returns(requester);
        _departmentRepository.GetByIdAsync(dept.Id, Arg.Any<CancellationToken>()).Returns(dept);
        _userRepository.GetByIdAsync(headId, Arg.Any<CancellationToken>()).Returns(head);
        _currencyRepository.GetByCodeAsync(request.CurrencyCode, Arg.Any<CancellationToken>()).Returns((Currency?)null);

        var command = new ResubmitRequestCommand(request.Id, requesterId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CurrencyErrors.NotFound(request.CurrencyCode));

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenCurrencyInactive_ReturnsCurrencyInactiveError()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var headId = Guid.NewGuid();
        var dept = new Department("IT", headId, 1_000_000m, 10_000_000m);
        var requester = new User("alice", "Alice", "alice@sure.com", dept.Id, UserRole.Employee);
        var head = new User("head", "Dept Head", "head@sure.com", dept.Id, UserRole.DepartmentHead);
        var request = CreateSentBackRequest(requesterId, headId, Guid.NewGuid());

        var currency = new Currency("USD", "US Dollar", 3500m);
        currency.Deactivate();

        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(requesterId, Arg.Any<CancellationToken>()).Returns(requester);
        _departmentRepository.GetByIdAsync(dept.Id, Arg.Any<CancellationToken>()).Returns(dept);
        _userRepository.GetByIdAsync(headId, Arg.Any<CancellationToken>()).Returns(head);
        _currencyRepository.GetByCodeAsync(request.CurrencyCode, Arg.Any<CancellationToken>()).Returns(currency);

        var command = new ResubmitRequestCommand(request.Id, requesterId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CurrencyErrors.Inactive("USD"));

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenWithdrawMethodNotFound_ReturnsWithdrawMethodNotFoundError()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var headId = Guid.NewGuid();
        var withdrawMethodId = Guid.NewGuid();
        var dept = new Department("IT", headId, 1_000_000m, 10_000_000m);
        var requester = new User("alice", "Alice", "alice@sure.com", dept.Id, UserRole.Employee);
        var head = new User("head", "Dept Head", "head@sure.com", dept.Id, UserRole.DepartmentHead);
        var currency = new Currency("MMK", "Myanmar Kyat", 1m);
        var request = CreateSentBackRequest(requesterId, headId, withdrawMethodId);

        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(requesterId, Arg.Any<CancellationToken>()).Returns(requester);
        _departmentRepository.GetByIdAsync(dept.Id, Arg.Any<CancellationToken>()).Returns(dept);
        _userRepository.GetByIdAsync(headId, Arg.Any<CancellationToken>()).Returns(head);
        _currencyRepository.GetByCodeAsync(request.CurrencyCode, Arg.Any<CancellationToken>()).Returns(currency);
        _withdrawMethodRepository.GetByIdAsync(withdrawMethodId, Arg.Any<CancellationToken>()).Returns((WithdrawMethod?)null);

        var command = new ResubmitRequestCommand(request.Id, requesterId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(WithdrawMethodErrors.NotFound);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenWithdrawMethodInactive_ReturnsWithdrawMethodInactiveError()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var headId = Guid.NewGuid();
        var withdrawMethodId = Guid.NewGuid();
        var dept = new Department("IT", headId, 1_000_000m, 10_000_000m);
        var requester = new User("alice", "Alice", "alice@sure.com", dept.Id, UserRole.Employee);
        var head = new User("head", "Dept Head", "head@sure.com", dept.Id, UserRole.DepartmentHead);
        var currency = new Currency("MMK", "Myanmar Kyat", 1m);
        var method = new WithdrawMethod("Deactivated Method");
        method.Deactivate();

        var request = CreateSentBackRequest(requesterId, headId, withdrawMethodId);

        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(requesterId, Arg.Any<CancellationToken>()).Returns(requester);
        _departmentRepository.GetByIdAsync(dept.Id, Arg.Any<CancellationToken>()).Returns(dept);
        _userRepository.GetByIdAsync(headId, Arg.Any<CancellationToken>()).Returns(head);
        _currencyRepository.GetByCodeAsync(request.CurrencyCode, Arg.Any<CancellationToken>()).Returns(currency);
        _withdrawMethodRepository.GetByIdAsync(withdrawMethodId, Arg.Any<CancellationToken>()).Returns(method);

        var command = new ResubmitRequestCommand(request.Id, requesterId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(WithdrawMethodErrors.Inactive("Deactivated Method"));

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenDomainResubmitFails_ReturnsFailureWithoutDispatchingOrSaving()
    {
        // Arrange: Request is Draft, not SentBack!
        var requesterId = Guid.NewGuid();
        var headId = Guid.NewGuid();
        var withdrawMethodId = Guid.NewGuid();
        var dept = new Department("IT", headId, 1_000_000m, 10_000_000m);
        var requester = new User("alice", "Alice", "alice@sure.com", dept.Id, UserRole.Employee);
        var head = new User("head", "Dept Head", "head@sure.com", dept.Id, UserRole.DepartmentHead);
        var currency = new Currency("MMK", "Myanmar Kyat", 1m);
        var method = new WithdrawMethod("Cash");

        var draftRequest = BudgetRequest.CreateDraft(
            requesterId, "Alice", BudgetRequestType.Standard, headId, "Bob Head",
            DateTime.UtcNow.AddDays(2), 50_000m, "MMK", "Test", "Alice", "Job", withdrawMethodId,
            false, null);

        _budgetRequestRepository.GetByIdAsync(draftRequest.Id, Arg.Any<CancellationToken>()).Returns(draftRequest);
        _userRepository.GetByIdAsync(requesterId, Arg.Any<CancellationToken>()).Returns(requester);
        _departmentRepository.GetByIdAsync(dept.Id, Arg.Any<CancellationToken>()).Returns(dept);
        _userRepository.GetByIdAsync(headId, Arg.Any<CancellationToken>()).Returns(head);
        _currencyRepository.GetByCodeAsync(draftRequest.CurrencyCode, Arg.Any<CancellationToken>()).Returns(currency);
        _withdrawMethodRepository.GetByIdAsync(withdrawMethodId, Arg.Any<CancellationToken>()).Returns(method);

        var command = new ResubmitRequestCommand(draftRequest.Id, requesterId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("Only sent-back requests can be resubmitted");

        await _dispatcher.DidNotReceive().DispatchAsync(
            Arg.Any<BudgetRequest>(),
            Arg.Any<RequestStatus>(),
            Arg.Any<Guid>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenValid_ResubmitsRequestDispatchesNotificationAndSaves()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var headId = Guid.NewGuid();
        var withdrawMethodId = Guid.NewGuid();
        var dept = new Department("IT", headId, 1_000_000m, 10_000_000m);
        var requester = new User("alice", "Alice", "alice@sure.com", dept.Id, UserRole.Employee);
        var head = new User("head", "Dept Head", "head@sure.com", dept.Id, UserRole.DepartmentHead);
        var currency = new Currency("MMK", "Myanmar Kyat", 1m);
        var method = new WithdrawMethod("Bank Transfer");

        var request = CreateSentBackRequest(requesterId, headId, withdrawMethodId);

        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(requesterId, Arg.Any<CancellationToken>()).Returns(requester);
        _departmentRepository.GetByIdAsync(dept.Id, Arg.Any<CancellationToken>()).Returns(dept);
        _userRepository.GetByIdAsync(headId, Arg.Any<CancellationToken>()).Returns(head);
        _currencyRepository.GetByCodeAsync(request.CurrencyCode, Arg.Any<CancellationToken>()).Returns(currency);
        _withdrawMethodRepository.GetByIdAsync(withdrawMethodId, Arg.Any<CancellationToken>()).Returns(method);

        var command = new ResubmitRequestCommand(request.Id, requesterId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.Status.Should().Be(RequestStatus.PendingDeptHead);

        await _dispatcher.Received(1).DispatchAsync(
            request,
            RequestStatus.SentBack,
            requesterId,
            actorName: null,
            comment: null,
            Arg.Any<CancellationToken>());

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PassesCancellationTokenToDependencies()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var headId = Guid.NewGuid();
        var withdrawMethodId = Guid.NewGuid();
        var dept = new Department("IT", headId, 1_000_000m, 10_000_000m);
        var requester = new User("alice", "Alice", "alice@sure.com", dept.Id, UserRole.Employee);
        var head = new User("head", "Dept Head", "head@sure.com", dept.Id, UserRole.DepartmentHead);
        var currency = new Currency("MMK", "Myanmar Kyat", 1m);
        var method = new WithdrawMethod("Bank Transfer");

        var request = CreateSentBackRequest(requesterId, headId, withdrawMethodId);

        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        _budgetRequestRepository.GetByIdAsync(request.Id, ct).Returns(request);
        _userRepository.GetByIdAsync(requesterId, ct).Returns(requester);
        _departmentRepository.GetByIdAsync(dept.Id, ct).Returns(dept);
        _userRepository.GetByIdAsync(headId, ct).Returns(head);
        _currencyRepository.GetByCodeAsync(request.CurrencyCode, ct).Returns(currency);
        _withdrawMethodRepository.GetByIdAsync(withdrawMethodId, ct).Returns(method);

        var command = new ResubmitRequestCommand(request.Id, requesterId);

        // Act
        var result = await _handler.Handle(command, ct);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _budgetRequestRepository.Received(1).GetByIdAsync(request.Id, ct);
        await _userRepository.Received(1).GetByIdAsync(requesterId, ct);
        await _departmentRepository.Received(1).GetByIdAsync(dept.Id, ct);
        await _currencyRepository.Received(1).GetByCodeAsync(request.CurrencyCode, ct);
        await _withdrawMethodRepository.Received(1).GetByIdAsync(withdrawMethodId, ct);
        await _dispatcher.Received(1).DispatchAsync(request, RequestStatus.SentBack, requesterId, null, null, ct);
        await _unitOfWork.Received(1).SaveChangesAsync(ct);
    }
}
