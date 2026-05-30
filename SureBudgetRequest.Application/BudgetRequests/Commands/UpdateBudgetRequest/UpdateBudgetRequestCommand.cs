using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;

namespace SureBudgetRequest.Application.BudgetRequests.Commands.UpdateBudgetRequest;

public sealed record UpdateBudgetRequestCommand(
    Guid BudgetRequestId,
    Guid ActorId,
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
    decimal? ManualExchangeRate = null,
    Guid? BudgetCategoryId = null,
    string? Note = null) : IRequest<Result>;

public sealed class UpdateBudgetRequestCommandHandler
    : IRequestHandler<UpdateBudgetRequestCommand, Result>
{
    private readonly IBudgetRequestRepository _repository;
    private readonly ICurrencyRepository _currencyRepository;
    private readonly IWithdrawMethodRepository _withdrawMethodRepository;
    private readonly IBudgetCategoryRepository _budgetCategoryRepository;
    private readonly IBudgetRequestModificationRepository _modificationRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateBudgetRequestCommandHandler(
        IBudgetRequestRepository repository,
        ICurrencyRepository currencyRepository,
        IWithdrawMethodRepository withdrawMethodRepository,
        IBudgetCategoryRepository budgetCategoryRepository,
        IBudgetRequestModificationRepository modificationRepository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _currencyRepository = currencyRepository;
        _withdrawMethodRepository = withdrawMethodRepository;
        _budgetCategoryRepository = budgetCategoryRepository;
        _modificationRepository = modificationRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(
        UpdateBudgetRequestCommand command,
        CancellationToken cancellationToken)
    {
        if (command.WithdrawMethodId == Guid.Empty)
            return Result.Failure(WithdrawMethodErrors.Required);

        var request = await _repository.GetByIdAsync(command.BudgetRequestId, cancellationToken);
        if (request is null)
            return Result.Failure(BudgetRequestErrors.NotFound(command.BudgetRequestId));

        // Authorization Logic:
        // 1. Requester can edit in Draft or SentBack status.
        // 2. Department Head (assigned at submission) can edit in PendingDeptHead status.
        bool isAuthorized = false;
        if (request.Status is RequestStatus.Draft or RequestStatus.SentBack)
        {
            isAuthorized = request.RequesterId == command.ActorId;
        }
        else if (request.Status == RequestStatus.PendingDeptHead)
        {
            isAuthorized = request.DeptHeadIdAtSubmission == command.ActorId;
        }

        if (!isAuthorized)
            return Result.Failure(Error.Forbidden("BudgetRequest.UpdateUnauthorized", "You are not authorized to update this request in its current status."));

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

        // Budget category is optional. Validate only when one was chosen — it
        // must exist and still be active.
        if (command.BudgetCategoryId.HasValue)
        {
            var category = await _budgetCategoryRepository.GetByIdAsync(
                command.BudgetCategoryId.Value, cancellationToken);
            if (category is null)
                return Result.Failure(BudgetCategoryErrors.NotFound);
            if (!category.IsActive)
                return Result.Failure(BudgetCategoryErrors.Inactive(category.Name));
        }

        var utcRequestDate = DateTime.SpecifyKind(command.RequestDate, DateTimeKind.Utc);

        var result = request.UpdateDetails(
            utcRequestDate,
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
            command.ManualExchangeRate,
            command.BudgetCategoryId);

        if (result.IsFailure) return result;

        // Record the modification in the audit trail.
        await _modificationRepository.AddAsync(
            new BudgetRequestModification(request.Id, command.ActorId, command.Note),
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
