using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;

namespace SureBudgetRequest.Application.Departments.Commands.DeactivateDepartment;

public sealed record DeactivateDepartmentCommand(Guid DepartmentId) : IRequest<Result>;

public sealed class DeactivateDepartmentCommandHandler
    : IRequestHandler<DeactivateDepartmentCommand, Result>
{
    private readonly IDepartmentRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public DeactivateDepartmentCommandHandler(IDepartmentRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(DeactivateDepartmentCommand command, CancellationToken ct)
    {
        var dept = await _repository.GetByIdAsync(command.DepartmentId, ct);
        if (dept is null) return Result.Failure("Department not found.");

        dept.Deactivate();
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
