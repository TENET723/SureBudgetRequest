using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;

namespace SureBudgetRequest.Application.Coas.Commands.ReactivateCoa;

public sealed record ReactivateCoaCommand(Guid CoaId) : IRequest<Result>;

public sealed class ReactivateCoaCommandHandler
    : IRequestHandler<ReactivateCoaCommand, Result>
{
    private readonly ICoaRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public ReactivateCoaCommandHandler(ICoaRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(ReactivateCoaCommand command, CancellationToken ct)
    {
        var coa = await _repository.GetByIdAsync(command.CoaId, ct);
        if (coa is null) return Result.Failure("Chart of Account not found.");

        coa.Reactivate();
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
