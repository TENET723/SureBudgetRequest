using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Errors;

namespace SureBudgetRequest.Application.Coas.Commands.CreateCoa;

public sealed record CreateCoaCommand(
    string Code,
    string Name,
    string? Description = null) : IRequest<Result<Guid>>;

public sealed class CreateCoaCommandHandler
    : IRequestHandler<CreateCoaCommand, Result<Guid>>
{
    private readonly ICoaRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateCoaCommandHandler(ICoaRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Guid>> Handle(CreateCoaCommand command, CancellationToken ct)
    {
        // Uniqueness check on Code — case-sensitive trim happens in the entity ctor.
        var existing = await _repository.GetByCodeAsync(command.Code, ct);
        if (existing is not null)
            return Result.Failure<Guid>(CoaErrors.AlreadyExists(existing.Code));

        Coa coa;
        try
        {
            coa = new Coa(command.Code, command.Name, command.Description);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<Guid>(CoaErrors.ValidationError(ex.Message));
        }

        await _repository.AddAsync(coa, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success(coa.Id);
    }
}
