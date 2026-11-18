using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.Abstractions.Security;
using SureBudgetRequest.Application.BudgetRequests.Common;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;

namespace SureBudgetRequest.Application.BudgetRequests.Commands.AssignAccountingCategory;

public sealed record AssignAccountingCategoryCommand(
    Guid BudgetRequestId,
    Guid AccountingCategoryId) : IRequest<Result>;

public sealed class AssignAccountingCategoryCommandHandler
    : IRequestHandler<AssignAccountingCategoryCommand, Result>
{
    private readonly IBudgetRequestRepository _budgetRequestRepository;
    private readonly IAccountingCategoryRepository _accountingCategoryRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IUserRepository _userRepository;
    private readonly INotificationDispatcher _dispatcher;
    private readonly IUnitOfWork _unitOfWork;

    public AssignAccountingCategoryCommandHandler(
        IBudgetRequestRepository budgetRequestRepository,
        IAccountingCategoryRepository accountingCategoryRepository,
        ICurrentUser currentUser,
        IUserRepository userRepository,
        INotificationDispatcher dispatcher,
        IUnitOfWork unitOfWork)
    {
        _budgetRequestRepository = budgetRequestRepository;
        _accountingCategoryRepository = accountingCategoryRepository;
        _currentUser = currentUser;
        _userRepository = userRepository;
        _dispatcher = dispatcher;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(
        AssignAccountingCategoryCommand command,
        CancellationToken cancellationToken)
    {
        if (_currentUser.Role != UserRole.Accounting)
            return Result.Failure(BudgetRequestErrors.OnlyAccountingCanCategorize);

        var budgetRequest = await _budgetRequestRepository.GetByIdAsync(
            command.BudgetRequestId, cancellationToken);
        if (budgetRequest is null)
            return Result.Failure(BudgetRequestErrors.NotFound(command.BudgetRequestId));

        var category = await _accountingCategoryRepository.GetByIdAsync(
            command.AccountingCategoryId, cancellationToken);
        if (category is null)
            return Result.Failure(AccountingCategoryErrors.GenericNotFound);
        if (!category.IsActive)
            return Result.Failure(AccountingCategoryErrors.Inactive(category.Name));

        var fallbackUserId = _currentUser.IsFallbackPersonAct ? _currentUser.RealActorUserId : (Guid?)null;
        var result = budgetRequest.SetAccountingCategory(
            command.AccountingCategoryId,
            _currentUser.UserId,
            category.Name,
            fallbackUserId);
        if (!result.IsSuccess)
            return result;

        var actor = await _userRepository.GetByIdAsync(_currentUser.UserId, cancellationToken);
        string actorMention;
        if (_currentUser.IsFallbackPersonAct)
        {
            var realActor = await _userRepository.GetByIdAsync(_currentUser.RealActorUserId, cancellationToken);
            var realMention = !string.IsNullOrWhiteSpace(realActor?.SlackUserId)
                ? $"<@{realActor.SlackUserId}>"
                : $"@{_currentUser.RealActorFullName}";
            var targetMention = !string.IsNullOrWhiteSpace(actor?.SlackUserId)
                ? $"<@{actor.SlackUserId}>"
                : $"@{actor?.FullName ?? "Accounting"}";
            actorMention = $"{realMention} (acting for {targetMention})";
        }
        else
        {
            actorMention = !string.IsNullOrWhiteSpace(actor?.SlackUserId)
                ? $"<@{actor.SlackUserId}>"
                : $"@{actor?.FullName ?? "Accounting"}";
        }

        await _dispatcher.DispatchAccountingCategorizedAsync(
            budgetRequest,
            category.Name,
            actorMention,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
