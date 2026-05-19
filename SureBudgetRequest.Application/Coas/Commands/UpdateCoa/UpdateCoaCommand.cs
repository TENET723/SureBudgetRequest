using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;

namespace SureBudgetRequest.Application.Coas.Commands.UpdateCoa;

public sealed record UpdateCoaCommand(
    Guid CoaId,
    string Code,
    string Name,
    string? Description,
    bool IsActive) : IRequest<Result>;

public sealed class UpdateCoaCommandHandler
    : IRequestHandler<UpdateCoaCommand, Result>
{
    private readonly ICoaRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateCoaCommandHandler(ICoaRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(UpdateCoaCommand command, CancellationToken ct)
    {
        var coa = await _repository.GetByIdAsync(command.CoaId, ct);
        if (coa is null) return Result.Failure("Chart of Account not found.");

        // If the code is being changed, ensure the new code isn't taken by another row.
        if (!string.Equals(coa.Code, command.Code?.Trim(), StringComparison.Ordinal))
        {
            var existing = await _repository.GetByCodeAsync(command.Code ?? string.Empty, ct);
            if (existing is not null && existing.Id != coa.Id)
                return Result.Failure($"Chart of Account with code '{existing.Code}' already exists.");
        }

        try
        {
            coa.ChangeCode(command.Code);
            coa.Rename(command.Name);
            coa.ChangeDescription(command.Description);
            if (command.IsActive) coa.Reactivate(); else coa.Deactivate();
        }
        catch (ArgumentException ex)
        {
            return Result.Failure(ex.Message);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
