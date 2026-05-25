using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Errors;

namespace SureBudgetRequest.Application.Coas.Commands.DeactivateCoa;

public sealed record DeactivateCoaCommand(Guid CoaId) : IRequest<Result>;

public sealed class DeactivateCoaCommandHandler
    : IRequestHandler<DeactivateCoaCommand, Result>
{
    private readonly ICoaRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public DeactivateCoaCommandHandler(ICoaRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(DeactivateCoaCommand command, CancellationToken ct)
    {
        var coa = await _repository.GetByIdAsync(command.CoaId, ct);
        if (coa is null) return Result.Failure(CoaErrors.NotFound);

        // Deactivation does NOT block existing budget_requests that reference this
        // Coa — historical data stays intact. New approvals just won't show it
        // in the picker.
        coa.Deactivate();
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
