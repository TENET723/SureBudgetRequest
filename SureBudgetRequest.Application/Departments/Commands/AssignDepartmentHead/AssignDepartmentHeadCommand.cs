using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;

namespace SureBudgetRequest.Application.Departments.Commands.AssignDepartmentHead;

public sealed record AssignDepartmentHeadCommand(
    Guid DepartmentId,
    Guid HeadUserId) : IRequest<Result>;

public sealed class AssignDepartmentHeadCommandHandler
    : IRequestHandler<AssignDepartmentHeadCommand, Result>
{
    private readonly IDepartmentRepository _departmentRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AssignDepartmentHeadCommandHandler(
        IDepartmentRepository departmentRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork)
    {
        _departmentRepository = departmentRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(AssignDepartmentHeadCommand command, CancellationToken ct)
    {
        var dept = await _departmentRepository.GetByIdAsync(command.DepartmentId, ct);
        if (dept is null) return Result.Failure("Department not found.");

        var head = await _userRepository.GetByIdAsync(command.HeadUserId, ct);
        if (head is null || !head.IsActive)
            return Result.Failure("User not found or is inactive.");

        dept.ChangeHead(command.HeadUserId);

        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
