using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.Abstractions.Services;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;

namespace SureBudgetRequest.Application.Users.Commands.UpdateUserFallback;

public sealed record UpdateUserFallbackCommand(
    Guid UserId,
    Guid? FallbackUserId,
    bool IsOnLeave) : IRequest<Result>;

public sealed class UpdateUserFallbackCommandHandler
    : IRequestHandler<UpdateUserFallbackCommand, Result>
{
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDelegationRevocationNotifier _delegationNotifier;

    public UpdateUserFallbackCommandHandler(
        IUserRepository userRepository,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork,
        IDelegationRevocationNotifier delegationNotifier)
    {
        _userRepository = userRepository;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _delegationNotifier = delegationNotifier;
    }

    public async Task<Result> Handle(UpdateUserFallbackCommand command, CancellationToken ct)
    {
        if (_currentUser.IsFallbackPersonAct)
            return Result.Failure(UserErrors.FallbackCannotModifyFallback);

        var realActorId = _currentUser.RealActorUserId != Guid.Empty
            ? _currentUser.RealActorUserId
            : _currentUser.UserId;

        if (command.UserId != realActorId && _currentUser.Role != UserRole.Admin)
            return Result.Failure(Error.Forbidden("User.Forbidden", "You can only update your own fallback settings."));

        var user = await _userRepository.GetByIdAsync(command.UserId, ct);
        if (user is null || !user.IsActive)
            return Result.Failure(UserErrors.GenericNotFound);

        if (command.FallbackUserId.HasValue)
        {
            if (command.FallbackUserId.Value == command.UserId)
                return Result.Failure(UserErrors.CannotAssignSelfAsFallback);

            var fallbackUser = await _userRepository.GetByIdAsync(command.FallbackUserId.Value, ct);
            if (fallbackUser is null || !fallbackUser.IsActive)
                return Result.Failure(Error.Validation("User.FallbackNotFound", "The selected fallback user is not found or is inactive."));
        }
        else if (command.IsOnLeave)
        {
            return Result.Failure(UserErrors.LeaveRequiresFallback);
        }

        var previousFallbackUserId = user.FallbackUserId;

        user.SetFallbackUser(command.FallbackUserId);
        user.SetOnLeave(command.IsOnLeave);

        await _unitOfWork.SaveChangesAsync(ct);

        var fallbackChanged = previousFallbackUserId.HasValue && previousFallbackUserId != user.FallbackUserId;
        var leaveRevoked = !user.IsOnLeave || !user.FallbackUserId.HasValue;

        if (fallbackChanged || leaveRevoked)
        {
            _delegationNotifier.NotifyRevocation(command.UserId);
        }

        return Result.Success();
    }
}
