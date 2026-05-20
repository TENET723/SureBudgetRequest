using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Enums;

namespace SureBudgetRequest.Application.BudgetRequests.Commands.UpdateDraft;

public sealed record UpdateDraftCommand(
    Guid BudgetRequestId,
    Guid RequesterId,
    BudgetRequestType Type,
    DateTime RequestDate,
    decimal RequestedAmount,
    string CurrencyCode,
    string Reasons,
    string WithdrawerName,
    string WithdrawerJobTitle,
    Guid WithdrawMethodId,
    bool AllowsPartialPayment,
    string? PartialPaymentDetail,
    string? MonthlyOverrunJustification = null) : IRequest<Result>;

public sealed class UpdateDraftCommandHandler
    : IRequestHandler<UpdateDraftCommand, Result>
{
    private readonly IBudgetRequestRepository _repository;
    private readonly ICurrencyRepository _currencyRepository;
    private readonly IWithdrawMethodRepository _withdrawMethodRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateDraftCommandHandler(
        IBudgetRequestRepository repository,
        ICurrencyRepository currencyRepository,
        IWithdrawMethodRepository withdrawMethodRepository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _currencyRepository = currencyRepository;
        _withdrawMethodRepository = withdrawMethodRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(
        UpdateDraftCommand command,
        CancellationToken cancellationToken)
    {
        if (command.WithdrawMethodId == Guid.Empty)
            return Result.Failure("Withdraw method is required.");

        var request = await _repository.GetByIdAsync(command.BudgetRequestId, cancellationToken);
        if (request is null)
            return Result.Failure("Budget request not found.");

        if (request.RequesterId != command.RequesterId)
            return Result.Failure("Only the requester can edit their draft.");

        // Validate currency on edit too — it could have been deactivated since the draft was created.
        var currency = await _currencyRepository.GetByCodeAsync(command.CurrencyCode, cancellationToken);
        if (currency is null)
            return Result.Failure($"Currency '{command.CurrencyCode}' not found.");
        if (!currency.IsActive)
            return Result.Failure($"Currency '{currency.Code}' is not active.");

        // Validate the chosen withdraw method exists and is still active.
        var withdrawMethod = await _withdrawMethodRepository.GetByIdAsync(
            command.WithdrawMethodId, cancellationToken);
        if (withdrawMethod is null)
            return Result.Failure("Selected withdraw method not found.");
        if (!withdrawMethod.IsActive)
            return Result.Failure(
                $"Withdraw method '{withdrawMethod.Name}' has been deactivated and cannot be used.");

        var result = request.UpdateDetails(
            command.RequestDate,
            command.Type,
            command.RequestedAmount,
            currency.Code,
            command.Reasons,
            command.WithdrawerName,
            command.WithdrawerJobTitle,
            command.WithdrawMethodId,
            command.AllowsPartialPayment,
            command.PartialPaymentDetail,
            command.MonthlyOverrunJustification);

        if (result.IsFailure) return result;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
