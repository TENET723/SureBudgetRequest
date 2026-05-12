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
    bool AllowsPartialPayment,
    string? PartialPaymentDetail) : IRequest<Result<Guid>>;

public sealed class CreateDraftCommandHandler
    : IRequestHandler<CreateDraftCommand, Result<Guid>>
{
    private readonly IBudgetRequestRepository _budgetRequestRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICurrencyRepository _currencyRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateDraftCommandHandler(
        IBudgetRequestRepository budgetRequestRepository,
        IUserRepository userRepository,
        ICurrencyRepository currencyRepository,
        IUnitOfWork unitOfWork)
    {
        _budgetRequestRepository = budgetRequestRepository;
        _userRepository = userRepository;
        _currencyRepository = currencyRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Guid>> Handle(
        CreateDraftCommand command,
        CancellationToken cancellationToken)
    {
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
                command.AllowsPartialPayment,
                command.PartialPaymentDetail);
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
