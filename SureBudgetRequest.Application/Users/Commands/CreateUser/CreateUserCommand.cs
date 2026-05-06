using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;

namespace SureBudgetRequest.Application.Users.Commands.CreateUser;

public sealed record CreateUserCommand(
    string Username,
    string FullName,
    Guid DepartmentId,
    UserRole Role,
    string? Email,
    string? SlackUserId) : IRequest<Result<Guid>>;

public sealed class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, Result<Guid>>
{
    private readonly IUserRepository _userRepository;
    private readonly IDepartmentRepository _departmentRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateUserCommandHandler(
        IUserRepository userRepository,
        IDepartmentRepository departmentRepository,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _departmentRepository = departmentRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Guid>> Handle(CreateUserCommand command, CancellationToken ct)
    {
        var dept = await _departmentRepository.GetByIdAsync(command.DepartmentId, ct);
        if (dept is null)
            return Result.Failure<Guid>("Department not found.");

        // R5: enforce single Boss
        if (command.Role == UserRole.Boss)
        {
            var existingBoss = await _userRepository.FindBossAsync(ct);
            if (existingBoss is not null)
                return Result.Failure<Guid>("A Boss is already assigned. Change the existing Boss role before assigning a new one.");
        }

        User user;
        try
        {
            user = new User(command.Username, command.FullName, command.DepartmentId, command.Role);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<Guid>(ex.Message);
        }

        user.SetEmail(command.Email);
        user.SetSlackUserId(command.SlackUserId);

        await _userRepository.AddAsync(user, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success(user.Id);
    }
}
