using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Entities;

namespace SureBudgetRequest.Application.Departments.Commands.CreateDepartment;

public sealed record CreateDepartmentCommand(
    string Name,
    Guid? HeadUserId,
    decimal BudgetLimit,
    decimal? MonthlyLimit = null,
    string? SlackWebhookUrl = null) : IRequest<Result<Guid>>;

public sealed class CreateDepartmentCommandHandler
    : IRequestHandler<CreateDepartmentCommand, Result<Guid>>
{
    private readonly IDepartmentRepository _departmentRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateDepartmentCommandHandler(
        IDepartmentRepository departmentRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork)
    {
        _departmentRepository = departmentRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Guid>> Handle(CreateDepartmentCommand command, CancellationToken ct)
    {
        // Only validate the head user exists when one was provided.
        // A department may be created with no head (bootstrap case, or vacancy).
        if (command.HeadUserId.HasValue)
        {
            var head = await _userRepository.GetByIdAsync(command.HeadUserId.Value, ct);
            if (head is null) return Result.Failure<Guid>("Department head user not found.");
        }

        Department dept;
        try
        {
            dept = new Department(
                command.Name,
                command.HeadUserId,
                command.BudgetLimit,
                command.MonthlyLimit,
                command.SlackWebhookUrl);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<Guid>(ex.Message);
        }

        await _departmentRepository.AddAsync(dept, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success(dept.Id);
    }
}
