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
    string Reasons,
    string WithdrawerName,
    string WithdrawerJobTitle,
    bool AllowsPartialPayment,
    string? PartialPaymentDetail) : IRequest<Result>;

public sealed class UpdateDraftCommandHandler
    : IRequestHandler<UpdateDraftCommand, Result>
{
    private readonly IBudgetRequestRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateDraftCommandHandler(
        IBudgetRequestRepository repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(
        UpdateDraftCommand command,
        CancellationToken cancellationToken)
    {
        var request = await _repository.GetByIdAsync(command.BudgetRequestId, cancellationToken);
        if (request is null)
            return Result.Failure("Budget request not found.");

        if (request.RequesterId != command.RequesterId)
            return Result.Failure("Only the requester can edit their draft.");

        var result = request.UpdateDetails(
            command.RequestDate,
            command.Type,
            command.RequestedAmount,
            command.Reasons,
            command.WithdrawerName,
            command.WithdrawerJobTitle,
            command.AllowsPartialPayment,
            command.PartialPaymentDetail);

        if (result.IsFailure) return result;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
