using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;

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
    string? MonthlyOverrunJustification = null) : IRequest<Result<Guid>>;

public sealed class CreateDraftCommandHandler
    : IRequestHandler<CreateDraftCommand, Result<Guid>>
{
    private readonly IBudgetRequestRepository _budgetRequestRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICurrencyRepository _currencyRepository;
    private readonly IWithdrawMethodRepository _withdrawMethodRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateDraftCommandHandler(
        IBudgetRequestRepository budgetRequestRepository,
        IUserRepository userRepository,
        ICurrencyRepository currencyRepository,
        IWithdrawMethodRepository withdrawMethodRepository,
        IUnitOfWork unitOfWork)
    {
        _budgetRequestRepository = budgetRequestRepository;
        _userRepository = userRepository;
        _currencyRepository = currencyRepository;
        _withdrawMethodRepository = withdrawMethodRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Guid>> Handle(
        CreateDraftCommand command,
        CancellationToken cancellationToken)
    {
        // Defense-in-depth: UI already blocks empty selection, but a direct
        // command send should still fail fast before hitting the DB.
        if (command.WithdrawMethodId == Guid.Empty)
            return Result.Failure<Guid>("Withdraw method is required.");

        var requester = await _userRepository.GetByIdAsync(command.RequesterId, cancellationToken);
        if (requester is null)
            return Result.Failure<Guid>("Requester not found.");

        if (!requester.IsActive)
            return Result.Failure<Guid>("Inactive users cannot create requests.");

        // Validate that the chosen currency exists and is active.
        var currency = await _currencyRepository.GetByCodeAsync(command.CurrencyCode, cancellationToken);
        if (currency is null)
            return Result.Failure<Guid>($"Currency '{command.CurrencyCode}' not found.");
        if (!currency.IsActive)
            return Result.Failure<Guid>($"Currency '{currency.Code}' is not active.");

        // Validate the chosen withdraw method exists and is still active.
        var withdrawMethod = await _withdrawMethodRepository.GetByIdAsync(
            command.WithdrawMethodId, cancellationToken);
        if (withdrawMethod is null)
            return Result.Failure<Guid>("Selected withdraw method not found.");
        if (!withdrawMethod.IsActive)
            return Result.Failure<Guid>(
                $"Withdraw method '{withdrawMethod.Name}' has been deactivated and cannot be used.");

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
                command.MonthlyOverrunJustification);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<Guid>(ex.Message);
        }

        await _budgetRequestRepository.AddAsync(draft, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(draft.Id);
    }
}
