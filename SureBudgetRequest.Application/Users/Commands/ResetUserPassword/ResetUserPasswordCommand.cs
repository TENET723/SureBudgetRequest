using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.Abstractions.Security;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Errors;

namespace SureBudgetRequest.Application.Users.Commands.ResetUserPassword;

/// <summary>
/// Admin sets a new initial password for a user (e.g. when the user forgets theirs).
/// Forces <c>MustChangePassword = true</c> so the user must change it on next login.
/// Authorization (caller must be Admin) is enforced in the Web layer.
/// </summary>
public sealed record ResetUserPasswordCommand(
    Guid UserId,
    string NewInitialPassword) : IRequest<Result>;

public sealed class ResetUserPasswordCommandHandler
    : IRequestHandler<ResetUserPasswordCommand, Result>
{
    public const int MinimumPasswordLength = 10;

    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;

    public ResetUserPasswordCommandHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(ResetUserPasswordCommand command, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(command.NewInitialPassword)
            || command.NewInitialPassword.Length < MinimumPasswordLength)
            return Result.Failure(UserErrors.PasswordTooShort(MinimumPasswordLength));

        var user = await _userRepository.GetByIdAsync(command.UserId, ct);
        if (user is null) return Result.Failure(UserErrors.GenericNotFound);

        user.SetPasswordHash(_passwordHasher.Hash(command.NewInitialPassword), mustChangeOnNextLogin: true);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}