using FluentAssertions;
using NSubstitute;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.BudgetRequests.Commands.RecordPayment;
using SureBudgetRequest.Application.BudgetRequests.Common;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;
using Xunit;

namespace SureBudgetRequest.Application.Tests.BudgetRequests.Commands;

public class RecordPaymentCommandHandlerTests
{
    private readonly IBudgetRequestRepository _budgetRequestRepository = Substitute.For<IBudgetRequestRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IBankAccountRepository _bankAccountRepository = Substitute.For<IBankAccountRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly INotificationDispatcher _dispatcher = Substitute.For<INotificationDispatcher>();
    private readonly RecordPaymentCommandHandler _handler;

    public RecordPaymentCommandHandlerTests()
    {
        _handler = new RecordPaymentCommandHandler(
            _budgetRequestRepository,
            _userRepository,
            _bankAccountRepository,
            _unitOfWork,
            _dispatcher);
    }

    private static BudgetRequest CreateApprovedStandardRequest(decimal amount = 200_000m, bool allowsPartial = false)
    {
        var headId = Guid.NewGuid();
        var request = BudgetRequest.CreateDraft(
            Guid.NewGuid(), "Alice", BudgetRequestType.Standard, headId, "Bob Head",
            DateTime.UtcNow.AddHours(6.5).Date.AddDays(2),
            amount, "MMK", "Supplies", "Alice", "Job", Guid.NewGuid(),
            allowsPartial, allowsPartial ? "Two tranches" : null);

        request.Submit(
            Guid.NewGuid(), 1_000_000m, 10_000_000m, 0m,
            DateTime.UtcNow, DateTime.UtcNow.AddMonths(1),
            1m, headId, "Bob Head", "Alice", false, 1);

        request.ApproveBy(headId);
        request.ApproveBy(Guid.NewGuid(), coaId: Guid.NewGuid()); // Approved

        return request;
    }

    [Fact]
    public async Task Handle_WhenBudgetRequestNotFound_ReturnsNotFoundError()
    {
        // Arrange
        var reqId = Guid.NewGuid();
        _budgetRequestRepository.GetByIdAsync(reqId, Arg.Any<CancellationToken>()).Returns((BudgetRequest?)null);

        var command = new RecordPaymentCommand(reqId, Guid.NewGuid(), 100_000m, DateTime.UtcNow, null, null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BudgetRequestErrors.NotFound(reqId));

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenFinanceUserNotFound_ReturnsOnlyFinanceCanRecordPaymentError()
    {
        // Arrange
        var request = CreateApprovedStandardRequest();
        var financeUserId = Guid.NewGuid();

        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(financeUserId, Arg.Any<CancellationToken>()).Returns((User?)null);

        var command = new RecordPaymentCommand(request.Id, financeUserId, 100_000m, DateTime.UtcNow, null, null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BudgetRequestErrors.OnlyFinanceCanRecordPayment);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(UserRole.Employee)]
    [InlineData(UserRole.DepartmentHead)]
    [InlineData(UserRole.Management)]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.Accounting)]
    [InlineData(UserRole.Support)]
    public async Task Handle_WhenUserIsNotFinanceRole_ReturnsOnlyFinanceCanRecordPaymentError(UserRole nonFinanceRole)
    {
        // Arrange
        var request = CreateApprovedStandardRequest();
        var userId = Guid.NewGuid();
        var user = new User("user", "User", "user@sure.com", Guid.NewGuid(), nonFinanceRole);

        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(user);

        var command = new RecordPaymentCommand(request.Id, userId, 100_000m, DateTime.UtcNow, null, null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BudgetRequestErrors.OnlyFinanceCanRecordPayment);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenSourceBankAccountIdNotFound_ReturnsBankAccountNotFoundError()
    {
        // Arrange
        var request = CreateApprovedStandardRequest();
        var financeUserId = Guid.NewGuid();
        var financeUser = new User("fin", "Finance", "fin@sure.com", Guid.NewGuid(), UserRole.Finance);
        var missingBankId = Guid.NewGuid();

        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(financeUserId, Arg.Any<CancellationToken>()).Returns(financeUser);
        _bankAccountRepository.GetByIdAsync(missingBankId, Arg.Any<CancellationToken>()).Returns((BankAccount?)null);

        var command = new RecordPaymentCommand(
            request.Id,
            financeUserId,
            100_000m,
            DateTime.UtcNow,
            "REF-01",
            "Payment note",
            SourceBankAccountId: missingBankId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BankAccountErrors.NotFound);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenSourceBankAccountInactive_ReturnsBankAccountInactiveError()
    {
        // Arrange
        var request = CreateApprovedStandardRequest();
        var financeUserId = Guid.NewGuid();
        var financeUser = new User("fin", "Finance", "fin@sure.com", Guid.NewGuid(), UserRole.Finance);

        var bankAccount = new BankAccount("KBZ Bank", "123456789", "Sure Co");
        bankAccount.Deactivate();

        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(financeUserId, Arg.Any<CancellationToken>()).Returns(financeUser);
        _bankAccountRepository.GetByIdAsync(bankAccount.Id, Arg.Any<CancellationToken>()).Returns(bankAccount);

        var command = new RecordPaymentCommand(
            request.Id,
            financeUserId,
            100_000m,
            DateTime.UtcNow,
            "REF-01",
            "Payment note",
            SourceBankAccountId: bankAccount.Id);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BankAccountErrors.Inactive("KBZ Bank"));

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenDomainPaymentFails_ReturnsFailureWithoutDispatchingOrSaving()
    {
        // Arrange: Attempting to pay more than the approved amount
        var request = CreateApprovedStandardRequest(amount: 100_000m, allowsPartial: true);
        var financeUserId = Guid.NewGuid();
        var financeUser = new User("fin", "Finance", "fin@sure.com", Guid.NewGuid(), UserRole.Finance);

        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(financeUserId, Arg.Any<CancellationToken>()).Returns(financeUser);

        var command = new RecordPaymentCommand(request.Id, financeUserId, 150_000m, DateTime.UtcNow, "OVER", "Too much");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("Payment exceeds remaining balance");

        await _dispatcher.DidNotReceive().DispatchAsync(
            Arg.Any<BudgetRequest>(),
            Arg.Any<RequestStatus>(),
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenPartialPaymentRecorded_SavesChangesWithoutDispatchingNotification()
    {
        // Arrange
        var request = CreateApprovedStandardRequest(amount: 200_000m, allowsPartial: true);
        var financeUserId = Guid.NewGuid();
        var financeUser = new User("fin", "Finance", "fin@sure.com", Guid.NewGuid(), UserRole.Finance);

        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(financeUserId, Arg.Any<CancellationToken>()).Returns(financeUser);

        var command = new RecordPaymentCommand(request.Id, financeUserId, 100_000m, DateTime.UtcNow, "PARTIAL-1", "Tranche 1");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.Status.Should().Be(RequestStatus.PartiallyPaid);
        request.TotalPaid.Should().Be(100_000m);

        await _dispatcher.DidNotReceive().DispatchAsync(
            Arg.Any<BudgetRequest>(),
            Arg.Any<RequestStatus>(),
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenFullyPaid_RecordsPaymentDispatchesNotificationAndSaves()
    {
        // Arrange
        var request = CreateApprovedStandardRequest(amount: 200_000m);
        var financeUserId = Guid.NewGuid();
        var financeUser = new User("fin", "Finance Payer", "fin@sure.com", Guid.NewGuid(), UserRole.Finance);

        var bankId = Guid.NewGuid();
        var bankAccount = new BankAccount("AYA Bank", "987654321", "Sure Corp");
        typeof(BankAccount).GetProperty(nameof(BankAccount.Id))!.SetValue(bankAccount, bankId);

        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(financeUserId, Arg.Any<CancellationToken>()).Returns(financeUser);
        _bankAccountRepository.GetByIdAsync(bankId, Arg.Any<CancellationToken>()).Returns(bankAccount);

        var command = new RecordPaymentCommand(
            request.Id,
            financeUserId,
            200_000m,
            DateTime.UtcNow,
            "FULL-PAY",
            "Disbursed in full",
            SourceBankAccountId: bankId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.Status.Should().Be(RequestStatus.Paid);
        request.TotalPaid.Should().Be(200_000m);

        await _dispatcher.Received(1).DispatchAsync(
            request,
            RequestStatus.Approved,
            financeUserId,
            "Finance Payer",
            null,
            Arg.Any<CancellationToken>());

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PassesCancellationTokenToDependencies()
    {
        // Arrange
        var request = CreateApprovedStandardRequest(amount: 200_000m);
        var financeUserId = Guid.NewGuid();
        var financeUser = new User("fin", "Finance Payer", "fin@sure.com", Guid.NewGuid(), UserRole.Finance);

        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        _budgetRequestRepository.GetByIdAsync(request.Id, ct).Returns(request);
        _userRepository.GetByIdAsync(financeUserId, ct).Returns(financeUser);

        var command = new RecordPaymentCommand(request.Id, financeUserId, 200_000m, DateTime.UtcNow, "FULL-PAY", null);

        // Act
        var result = await _handler.Handle(command, ct);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _budgetRequestRepository.Received(1).GetByIdAsync(request.Id, ct);
        await _userRepository.Received(1).GetByIdAsync(financeUserId, ct);
        await _dispatcher.Received(1).DispatchAsync(request, RequestStatus.Approved, financeUserId, "Finance Payer", null, ct);
        await _unitOfWork.Received(1).SaveChangesAsync(ct);
    }
}
