using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;

namespace SureBudgetRequest.Application.WithdrawMethods.Commands.ReactivateWithdrawMethod;

public sealed record ReactivateWithdrawMethodCommand(Guid WithdrawMethodId) : IRequest<Result>;

public sealed class ReactivateWithdrawMethodCommandHandler
    : IRequestHandler<ReactivateWithdrawMethodCommand, Result>
{
    private readonly IWithdrawMethodRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public ReactivateWithdrawMethodCommandHandler(IWithdrawMethodRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(ReactivateWithdrawMethodCommand command, CancellationToken ct)
    {
        var method = await _repository.GetByIdAsync(command.WithdrawMethodId, ct);
        if (method is null) return Result.Failure("Withdraw method not found.");

        method.Reactivate();
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
