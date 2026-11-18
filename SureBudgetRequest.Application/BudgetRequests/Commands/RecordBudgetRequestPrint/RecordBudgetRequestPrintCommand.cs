using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.Abstractions.Security;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Errors;

namespace SureBudgetRequest.Application.BudgetRequests.Commands.RecordBudgetRequestPrint;

public sealed record RecordBudgetRequestPrintCommand(Guid BudgetRequestId) : IRequest<Result>;

public sealed class RecordBudgetRequestPrintCommandHandler
    : IRequestHandler<RecordBudgetRequestPrintCommand, Result>
{
    private readonly IBudgetRequestRepository _budgetRequestRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public RecordBudgetRequestPrintCommandHandler(
        IBudgetRequestRepository budgetRequestRepository,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork)
    {
        _budgetRequestRepository = budgetRequestRepository;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(
        RecordBudgetRequestPrintCommand command,
        CancellationToken cancellationToken)
    {
        var budgetRequest = await _budgetRequestRepository.GetByIdAsync(
            command.BudgetRequestId, cancellationToken);
        if (budgetRequest is null)
            return Result.Failure(BudgetRequestErrors.NotFound(command.BudgetRequestId));

        var result = budgetRequest.RecordPrint(_currentUser.UserId);
        if (!result.IsSuccess)
            return result;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
