using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Errors;

namespace SureBudgetRequest.Application.WithdrawMethods.Commands.DeactivateWithdrawMethod;

public sealed record DeactivateWithdrawMethodCommand(Guid WithdrawMethodId) : IRequest<Result>;

public sealed class DeactivateWithdrawMethodCommandHandler
    : IRequestHandler<DeactivateWithdrawMethodCommand, Result>
{
    private readonly IWithdrawMethodRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public DeactivateWithdrawMethodCommandHandler(IWithdrawMethodRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(DeactivateWithdrawMethodCommand command, CancellationToken ct)
    {
        var method = await _repository.GetByIdAsync(command.WithdrawMethodId, ct);
        if (method is null) return Result.Failure(WithdrawMethodErrors.GenericNotFound);

        method.Deactivate();
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}