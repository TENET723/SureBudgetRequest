using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;

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
    string? MonthlyOverrunJustification = null,
    decimal? ManualExchangeRate = null) : IRequest<Result>;

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
            return Result.Failure(WithdrawMethodErrors.Required);

        var request = await _repository.GetByIdAsync(command.BudgetRequestId, cancellationToken);
        if (request is null)
            return Result.Failure(BudgetRequestErrors.NotFound(command.BudgetRequestId));

        if (request.RequesterId != command.RequesterId)
            return Result.Failure(BudgetRequestErrors.OnlyRequesterCanEditDraft);

        // Validate currency on edit too — it could have been deactivated since the draft was created.
        var currency = await _currencyRepository.GetByCodeAsync(command.CurrencyCode, cancellationToken);
        if (currency is null)
            return Result.Failure(CurrencyErrors.NotFound(command.CurrencyCode));
        if (!currency.IsActive)
            return Result.Failure(CurrencyErrors.Inactive(currency.Code));

        // Validate the chosen withdraw method exists and is still active.
        var withdrawMethod = await _withdrawMethodRepository.GetByIdAsync(
            command.WithdrawMethodId, cancellationToken);
        if (withdrawMethod is null)
            return Result.Failure(WithdrawMethodErrors.NotFound);
        if (!withdrawMethod.IsActive)
            return Result.Failure(WithdrawMethodErrors.Inactive(withdrawMethod.Name));

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
            command.MonthlyOverrunJustification,
            command.ManualExchangeRate);

        if (result.IsFailure) return result;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
