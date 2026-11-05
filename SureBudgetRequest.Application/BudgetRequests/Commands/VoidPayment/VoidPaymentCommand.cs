using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.BudgetRequests.Common;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;

namespace SureBudgetRequest.Application.BudgetRequests.Commands.VoidPayment;

public sealed record VoidPaymentCommand(
    Guid BudgetRequestId,
    Guid PaymentId,
    Guid FinanceUserId) : IRequest<Result>;

public sealed class VoidPaymentCommandHandler
    : IRequestHandler<VoidPaymentCommand, Result>
{
    private readonly IBudgetRequestRepository _budgetRequestRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationDispatcher _dispatcher;
    private readonly ICurrentUser? _currentUser;

    public VoidPaymentCommandHandler(
        IBudgetRequestRepository budgetRequestRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        INotificationDispatcher dispatcher,
        ICurrentUser? currentUser = null)
    {
        _budgetRequestRepository = budgetRequestRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _dispatcher = dispatcher;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(
        VoidPaymentCommand command,
        CancellationToken cancellationToken)
    {
        var budgetRequest = await _budgetRequestRepository.GetByIdAsync(
            command.BudgetRequestId, cancellationToken);
        if (budgetRequest is null)
            return Result.Failure(BudgetRequestErrors.NotFound(command.BudgetRequestId));

        var financeUser = await _userRepository.GetByIdAsync(command.FinanceUserId, cancellationToken);
        if (financeUser is null || financeUser.Role != UserRole.Finance)
            return Result.Failure(BudgetRequestErrors.OnlyFinanceCanVoidPayment);

        var payment = budgetRequest.Payments.FirstOrDefault(p => p.Id == command.PaymentId);
        if (payment is null)
            return Result.Failure("Payment not found on this request.");

        var paymentAmount = payment.Amount;
        var currencyCode = budgetRequest.CurrencyCode;

        var voidResult = budgetRequest.VoidPayment(command.PaymentId, command.FinanceUserId, DateTime.UtcNow);
        if (voidResult.IsFailure)
            return voidResult;

        var actorName = _currentUser?.IsFallbackPersonAct == true
            ? $"{_currentUser.RealActorFullName} (acting for {financeUser.FullName})"
            : financeUser.FullName;

        await _dispatcher.DispatchPaymentVoidedAsync(
            budgetRequest,
            paymentAmount,
            currencyCode,
            actorName,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
