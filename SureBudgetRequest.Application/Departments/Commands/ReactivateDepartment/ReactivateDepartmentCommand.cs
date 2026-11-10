using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Errors;

namespace SureBudgetRequest.Application.Departments.Commands.ReactivateDepartment;

public sealed record ReactivateDepartmentCommand(Guid DepartmentId) : IRequest<Result>;

public sealed class ReactivateDepartmentCommandHandler
    : IRequestHandler<ReactivateDepartmentCommand, Result>
{
    private readonly IDepartmentRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public ReactivateDepartmentCommandHandler(IDepartmentRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(ReactivateDepartmentCommand command, CancellationToken ct)
    {
        var dept = await _repository.GetByIdAsync(command.DepartmentId, ct);
        if (dept is null) return Result.Failure(DepartmentErrors.NotFound);

        dept.Reactivate();
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
