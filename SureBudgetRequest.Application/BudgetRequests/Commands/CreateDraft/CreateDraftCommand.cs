using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;

namespace SureBudgetRequest.Application.BudgetRequests.Commands.CreateDraft;

public sealed record CreateDraftCommand(
    Guid RequesterId,
    Guid DeptHeadId,
    BudgetRequestType Type,
    string DeptHeadName,
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
    Guid? BudgetCategoryId = null) : IRequest<Result<Guid>>;

public sealed class CreateDraftCommandHandler
    : IRequestHandler<CreateDraftCommand, Result<Guid>>
{
    private readonly IBudgetRequestRepository _budgetRequestRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICurrencyRepository _currencyRepository;
    private readonly IWithdrawMethodRepository _withdrawMethodRepository;
    private readonly IBudgetCategoryRepository _budgetCategoryRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateDraftCommandHandler(
        IBudgetRequestRepository budgetRequestRepository,
        IUserRepository userRepository,
        ICurrencyRepository currencyRepository,
        IWithdrawMethodRepository withdrawMethodRepository,
        IBudgetCategoryRepository budgetCategoryRepository,
        IUnitOfWork unitOfWork)
    {
        _budgetRequestRepository = budgetRequestRepository;
        _userRepository = userRepository;
        _currencyRepository = currencyRepository;
        _withdrawMethodRepository = withdrawMethodRepository;
        _budgetCategoryRepository = budgetCategoryRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Guid>> Handle(
        CreateDraftCommand command,
        CancellationToken cancellationToken)
    {
        // Defense-in-depth: UI already blocks empty selection, but a direct
        // command send should still fail fast before hitting the DB.
        if (command.WithdrawMethodId == Guid.Empty)
            return Result.Failure<Guid>(WithdrawMethodErrors.Required);

        var requester = await _userRepository.GetByIdAsync(command.RequesterId, cancellationToken);
        if (requester is null)
            return Result.Failure<Guid>(UserErrors.NotFound(command.RequesterId));

        if (!requester.IsActive)
            return Result.Failure<Guid>(UserErrors.Inactive);

        // Validate that the chosen currency exists and is active.
        var currency = await _currencyRepository.GetByCodeAsync(command.CurrencyCode, cancellationToken);
        if (currency is null)
            return Result.Failure<Guid>(CurrencyErrors.NotFound(command.CurrencyCode));
        if (!currency.IsActive)
            return Result.Failure<Guid>(CurrencyErrors.Inactive(currency.Code));

        // Validate the chosen withdraw method exists and is still active.
        var withdrawMethod = await _withdrawMethodRepository.GetByIdAsync(
            command.WithdrawMethodId, cancellationToken);
        if (withdrawMethod is null)
            return Result.Failure<Guid>(WithdrawMethodErrors.NotFound);
        if (!withdrawMethod.IsActive)
            return Result.Failure<Guid>(WithdrawMethodErrors.Inactive(withdrawMethod.Name));

        // Budget category is optional. Validate only when one was chosen — it
        // must exist and still be active.
        if (command.BudgetCategoryId.HasValue)
        {
            var category = await _budgetCategoryRepository.GetByIdAsync(
                command.BudgetCategoryId.Value, cancellationToken);
            if (category is null)
                return Result.Failure<Guid>(BudgetCategoryErrors.NotFound);
            if (!category.IsActive)
                return Result.Failure<Guid>(BudgetCategoryErrors.Inactive(category.Name));
        }

        var utcRequestDate = DateTime.SpecifyKind(command.RequestDate, DateTimeKind.Utc);

        BudgetRequest draft;
        try
        {
            draft = BudgetRequest.CreateDraft(
                command.RequesterId,
                requester.FullName,
                command.Type,
                command.DeptHeadId,
                command.DeptHeadName,
                utcRequestDate,
                command.RequestedAmount,
                currency.Code,                  // canonical, upper-cased form
                command.Reasons,
                command.WithdrawerName,
                command.WithdrawerJobTitle,
                command.WithdrawMethodId,
                command.AllowsPartialPayment,
                command.PartialPaymentDetail,
                command.MonthlyOverrunJustification,
                command.ManualExchangeRate,
                command.BudgetCategoryId);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<Guid>(Error.Validation("BudgetRequest.CreateError", ex.Message));
        }

        await _budgetRequestRepository.AddAsync(draft, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(draft.Id);
    }
}
