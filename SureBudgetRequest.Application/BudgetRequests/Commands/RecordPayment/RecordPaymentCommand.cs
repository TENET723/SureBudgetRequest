using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.BudgetRequests.Common;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;

namespace SureBudgetRequest.Application.BudgetRequests.Commands.RecordPayment;

public sealed record RecordPaymentCommand(
    Guid BudgetRequestId,
    Guid FinanceUserId,
    decimal Amount,
    DateTime PaidAt,
    string? Reference,
    string? Note,
    Guid? AttachmentId = null,
    Guid? SourceBankAccountId = null) : IRequest<Result>;

public sealed class RecordPaymentCommandHandler
    : IRequestHandler<RecordPaymentCommand, Result>
{
    private readonly IBudgetRequestRepository _budgetRequestRepository;
    private readonly IUserRepository _userRepository;
    private readonly IBankAccountRepository _bankAccountRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationDispatcher _dispatcher;

    public RecordPaymentCommandHandler(
        IBudgetRequestRepository budgetRequestRepository,
        IUserRepository userRepository,
        IBankAccountRepository bankAccountRepository,
        IUnitOfWork unitOfWork,
        INotificationDispatcher dispatcher)
    {
        _budgetRequestRepository = budgetRequestRepository;
        _userRepository = userRepository;
        _bankAccountRepository = bankAccountRepository;
        _unitOfWork = unitOfWork;
        _dispatcher = dispatcher;
    }

    public async Task<Result> Handle(
        RecordPaymentCommand command,
        CancellationToken cancellationToken)
    {
        var budgetRequest = await _budgetRequestRepository.GetByIdAsync(
            command.BudgetRequestId, cancellationToken);
        if (budgetRequest is null)
            return Result.Failure(BudgetRequestErrors.NotFound(command.BudgetRequestId));

        var financeUser = await _userRepository.GetByIdAsync(command.FinanceUserId, cancellationToken);
        if (financeUser is null || financeUser.Role != UserRole.Finance)
            return Result.Failure(BudgetRequestErrors.OnlyFinanceCanRecordPayment);

        // Source bank account is optional (null for cash). When supplied, load it,
        // ensure it's usable, and snapshot its details onto the payment row so the
        // payment history stays stable if the account is later edited/deactivated.
        Guid? sourceBankAccountId = null;
        string? sourceBankName = null;
        string? sourceAccountNumber = null;
        string? sourceAccountHolderName = null;
        if (command.SourceBankAccountId.HasValue)
        {
            var account = await _bankAccountRepository.GetByIdAsync(command.SourceBankAccountId.Value, cancellationToken);
            if (account is null)
                return Result.Failure(BankAccountErrors.NotFound);
            if (!account.IsActive)
                return Result.Failure(BankAccountErrors.Inactive(account.BankName));

            sourceBankAccountId = account.Id;
            sourceBankName = account.BankName;
            sourceAccountNumber = account.AccountNumber;
            sourceAccountHolderName = account.AccountHolderName;
        }

        var previousStatus = budgetRequest.Status;
        var result = budgetRequest.RecordPayment(
            command.Amount,
            command.FinanceUserId,
            DateTime.SpecifyKind(command.PaidAt, DateTimeKind.Utc),
            command.Reference,
            command.Note,
            command.AttachmentId,
            sourceBankAccountId,
            sourceBankName,
            sourceAccountNumber,
            sourceAccountHolderName);

        if (result.IsFailure) return result;

        // Only notify when fully paid (status changed to Paid)
        if (budgetRequest.Status == RequestStatus.Paid)
        {
            await _dispatcher.DispatchAsync(
                budgetRequest,
                previousStatus,
                command.FinanceUserId,
                actorName: financeUser.FullName,
                comment: null,
                cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
