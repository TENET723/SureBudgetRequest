using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.Abstractions.Security;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Errors;

namespace SureBudgetRequest.Application.Users.Commands.ChangePassword;

/// <summary>
/// Self-service password change for the currently logged-in user.
/// Used both for the forced first-login change and for voluntary changes later.
/// Requires the current password so a hijacked session cannot silently lock out the owner.
/// </summary>
public sealed record ChangePasswordCommand(
    Guid UserId,
    string CurrentPassword,
    string NewPassword) : IRequest<Result>;

public sealed class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, Result>
{
    public const int MinimumPasswordLength = 6;

    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;

    public ChangePasswordCommandHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(ChangePasswordCommand command, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(command.NewPassword)
            || command.NewPassword.Length < MinimumPasswordLength)
            return Result.Failure(UserErrors.PasswordTooShort(MinimumPasswordLength));

        if (command.CurrentPassword == command.NewPassword)
            return Result.Failure(UserErrors.PasswordsMustBeDifferent);

        var user = await _userRepository.GetByIdAsync(command.UserId, ct);
        if (user is null || !user.IsActive)
            return Result.Failure(UserErrors.GenericNotFound);

        if (!_passwordHasher.Verify(user.PasswordHash, command.CurrentPassword))
            return Result.Failure(UserErrors.CurrentPasswordIncorrect);

        user.SetPasswordHash(_passwordHasher.Hash(command.NewPassword), mustChangeOnNextLogin: false);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
