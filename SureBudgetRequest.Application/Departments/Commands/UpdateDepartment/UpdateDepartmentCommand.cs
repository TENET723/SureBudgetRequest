using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;

namespace SureBudgetRequest.Application.Departments.Commands.UpdateDepartment;

public sealed record UpdateDepartmentCommand(
    Guid DepartmentId,
    string Name,
    Guid? HeadUserId,
    decimal BudgetLimit) : IRequest<Result>;

public sealed class UpdateDepartmentCommandHandler
    : IRequestHandler<UpdateDepartmentCommand, Result>
{
    private readonly IDepartmentRepository _departmentRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateDepartmentCommandHandler(
        IDepartmentRepository departmentRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork)
    {
        _departmentRepository = departmentRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(UpdateDepartmentCommand command, CancellationToken ct)
    {
        var dept = await _departmentRepository.GetByIdAsync(command.DepartmentId, ct);
        if (dept is null) return Result.Failure("Department not found.");

        // Only validate the head user exists when one was provided.
        // Passing null clears the head (e.g. position vacant).
        if (command.HeadUserId.HasValue)
        {
            var head = await _userRepository.GetByIdAsync(command.HeadUserId.Value, ct);
            if (head is null) return Result.Failure("Department head user not found.");
        }

        dept.Rename(command.Name);
        dept.ChangeHead(command.HeadUserId);
        dept.ChangeBudgetLimit(command.BudgetLimit);

        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
