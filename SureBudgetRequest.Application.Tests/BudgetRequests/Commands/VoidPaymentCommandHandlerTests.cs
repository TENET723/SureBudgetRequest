using FluentAssertions;
using NSubstitute;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.BudgetRequests.Commands.VoidPayment;
using SureBudgetRequest.Application.BudgetRequests.Common;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;
using Xunit;

namespace SureBudgetRequest.Application.Tests.BudgetRequests.Commands;

public class VoidPaymentCommandHandlerTests
{
    private readonly IBudgetRequestRepository _budgetRequestRepository = Substitute.For<IBudgetRequestRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly INotificationDispatcher _dispatcher = Substitute.For<INotificationDispatcher>();
    private readonly VoidPaymentCommandHandler _handler;

    public VoidPaymentCommandHandlerTests()
    {
        _handler = new VoidPaymentCommandHandler(
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

    private static (BudgetRequest Request, Payment Payment) CreatePaidRequest(Guid financeId)
    {
        var headId = Guid.NewGuid();
        var request = BudgetRequest.CreateDraft(
            Guid.NewGuid(), "Alice", BudgetRequestType.Standard, headId, "Bob Head",
            DateTime.UtcNow.AddDays(2),
            100_000m, "MMK", "Supplies", "Alice", "Job", Guid.NewGuid(),
            false, null);

        request.Submit(
            Guid.NewGuid(), 1_000_000m, 10_000_000m, 0m,
            DateTime.UtcNow, DateTime.UtcNow.AddMonths(1),
            1m, headId, "Bob Head", "Alice", false, 1);

        request.ApproveBy(headId);
        request.ApproveBy(financeId, coaId: Guid.NewGuid());
        request.RecordPayment(100_000m, DateTime.UtcNow, financeId, "REF-1", "Full payment");

        return (request, request.Payments.Single());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_WhenReasonIsNullOrWhiteSpace_ReturnsVoidReasonRequiredError(string? reason)
    {
        var result = await _handler.Handle(
            new VoidPaymentCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), reason!),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BudgetRequestErrors.VoidReasonRequired);
        await _budgetRequestRepository.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenRequestNotFound_ReturnsNotFoundError()
    {
        var reqId = Guid.NewGuid();
        _budgetRequestRepository.GetByIdAsync(reqId, Arg.Any<CancellationToken>()).Returns((BudgetRequest?)null);

        var result = await _handler.Handle(new VoidPaymentCommand(reqId, Guid.NewGuid(), Guid.NewGuid(), "Void reason"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BudgetRequestErrors.NotFound(reqId));
    }

    [Fact]
    public async Task Handle_WhenUserNotFinance_ReturnsOnlyFinanceCanVoidPaymentError()
    {
        var financeId = Guid.NewGuid();
        var (request, payment) = CreatePaidRequest(financeId);
        var nonFinance = CreateUserWithId(Guid.NewGuid(), UserRole.Employee);

        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(nonFinance.Id, Arg.Any<CancellationToken>()).Returns(nonFinance);

        var result = await _handler.Handle(new VoidPaymentCommand(request.Id, payment.Id, nonFinance.Id, "Void reason"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BudgetRequestErrors.OnlyFinanceCanVoidPayment);
    }

    [Fact]
    public async Task Handle_WhenValidFinanceUserWithReason_SuccessfullyVoidsAndSavesReason()
    {
        var financeId = Guid.NewGuid();
        var (request, payment) = CreatePaidRequest(financeId);
        var finance = CreateUserWithId(financeId, UserRole.Finance);

        _budgetRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(finance.Id, Arg.Any<CancellationToken>()).Returns(finance);

        var result = await _handler.Handle(
            new VoidPaymentCommand(request.Id, payment.Id, finance.Id, "Wrong bank account used"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        payment.IsVoided.Should().BeTrue();
        payment.VoidReason.Should().Be("Wrong bank account used");
        request.Status.Should().Be(RequestStatus.Approved);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _dispatcher.Received(1).DispatchPaymentVoidedAsync(
            request, 100_000m, "MMK", finance.FullName, Arg.Any<CancellationToken>());
    }
}
