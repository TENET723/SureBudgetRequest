using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;

namespace SureBudgetRequest.Application.Users.Commands.UpdateUser;

public sealed record UpdateUserCommand(
    Guid UserId,
    string FullName,
    string Email,
    Guid DepartmentId,
    UserRole Role,
    string? SlackUserId,
    bool IsFinanceApprover = false) : IRequest<Result>;

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
            return Result.Failure(UserErrors.EmailRequired);

        var user = await _userRepository.GetByIdAsync(command.UserId, ct);
        if (user is null) return Result.Failure(UserErrors.GenericNotFound);

        var dept = await _departmentRepository.GetByIdAsync(command.DepartmentId, ct);
        if (dept is null) return Result.Failure(DepartmentErrors.NotFound);

        // If the email is changing, ensure the new one isn't already taken.
        var normalizedNew = command.Email.Trim().ToLowerInvariant();
        if (!string.Equals(user.Email, normalizedNew, StringComparison.Ordinal)
            && await _userRepository.EmailExistsAsync(normalizedNew, ct))
        {
            return Result.Failure(UserErrors.EmailAlreadyExists);
        }

        // The Finance-approver flag only makes sense for Finance users.
        if (command.IsFinanceApprover && command.Role != UserRole.Finance)
            return Result.Failure(UserErrors.OnlyFinanceCanBeApprover);

        // ── Bus-factor safeguard ───────────────────────────────────────────────
        // If this user is currently the LAST active Finance approver and the
        // update would strip them of that capability (role change away from
        // Finance, OR approver flag flipped off), block the save so the queue
        // never ends up with zero approvers.
        var wasApprover = user.IsActive && user.Role == UserRole.Finance && user.IsFinanceApprover;
        var willStillBeApprover = command.Role == UserRole.Finance && command.IsFinanceApprover;

        if (wasApprover && !willStillBeApprover)
        {
            var approverCount = await _userRepository.CountActiveFinanceApproversAsync(ct);
            if (approverCount <= 1)
            {
                return Result.Failure(UserErrors.LastActiveFinanceApprover);
            }
        }
        // ───────────────────────────────────────────────────────────────────────

        user.Rename(command.FullName);
        user.ChangeDepartment(command.DepartmentId);
        user.ChangeRole(command.Role); // also clears IsFinanceApprover when role != Finance
        user.SetEmail(command.Email);
        user.SetSlackUserId(command.SlackUserId);

        if (command.Role == UserRole.Finance)
            user.SetFinanceApprover(command.IsFinanceApprover);

        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}