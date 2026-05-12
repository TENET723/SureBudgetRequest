using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Enums;

namespace SureBudgetRequest.Application.Users.Commands.UpdateUser;

public sealed record UpdateUserCommand(
    Guid UserId,
    string FullName,
    string Email,
    Guid DepartmentId,
    UserRole Role,
    string? SlackUserId) : IRequest<Result>;

public sealed class UpdateUserCommandHandler : IRequestHandler<UpdateUserCommand, Result>
{
    private readonly IUserRepository _userRepository;
    private readonly IDepartmentRepository _departmentRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateUserCommandHandler(
        IUserRepository userRepository,
        IDepartmentRepository departmentRepository,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _departmentRepository = departmentRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(UpdateUserCommand command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.Email))
            return Result.Failure("Email is required.");

        var user = await _userRepository.GetByIdAsync(command.UserId, ct);
        if (user is null) return Result.Failure("User not found.");

        var dept = await _departmentRepository.GetByIdAsync(command.DepartmentId, ct);
        if (dept is null) return Result.Failure("Department not found.");

        // If the email is changing, ensure the new one isn't already taken.
        var normalizedNew = command.Email.Trim().ToLowerInvariant();
        if (!string.Equals(user.Email, normalizedNew, StringComparison.Ordinal)
            && await _userRepository.EmailExistsAsync(normalizedNew, ct))
        {
            return Result.Failure("A user with this email already exists.");
        }

        // R5: if promoting to Boss, ensure no other Boss exists
        if (command.Role == UserRole.Boss && user.Role != UserRole.Boss)
        {
            var existingBoss = await _userRepository.FindBossAsync(ct);
            if (existingBoss is not null && existingBoss.Id != user.Id)
                return Result.Failure("A Boss is already assigned.");
        }

        user.Rename(command.FullName);
        user.ChangeDepartment(command.DepartmentId);
        user.ChangeRole(command.Role);
        user.SetEmail(command.Email);
        user.SetSlackUserId(command.SlackUserId);

        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
