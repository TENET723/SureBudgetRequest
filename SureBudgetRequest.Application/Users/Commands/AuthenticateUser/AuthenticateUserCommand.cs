using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.Abstractions.Security;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Enums;

namespace SureBudgetRequest.Application.Users.Commands.AuthenticateUser;

/// <summary>
/// Verifies email + password and returns identity info for the authenticated user.
/// Does NOT issue the auth cookie — that is the Web layer's responsibility.
/// This command's only job is to answer "is this person who they claim to be?".
/// </summary>
public sealed record AuthenticateUserCommand(string Email, string Password)
    : IRequest<Result<AuthenticatedUserDto>>;

public sealed record AuthenticatedUserDto(
    Guid Id,
    string Username,
    string Email,
    string FullName,
    UserRole Role,
    Guid DepartmentId,
    bool MustChangePassword,
    bool IsFinanceApprover);

public sealed class AuthenticateUserCommandHandler
    : IRequestHandler<AuthenticateUserCommand, Result<AuthenticatedUserDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;

    public AuthenticateUserCommandHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<AuthenticatedUserDto>> Handle(
        AuthenticateUserCommand command,
        CancellationToken ct)
    {
        // Generic message for every failure mode — never reveal whether the email
        // exists or whether the password was wrong. Also covers inactive accounts.
        const string genericFailure = "Invalid email or password.";

        if (string.IsNullOrWhiteSpace(command.Email) || string.IsNullOrWhiteSpace(command.Password))
            return Result.Failure<AuthenticatedUserDto>(genericFailure);

        var user = await _userRepository.GetByEmailAsync(command.Email, ct);
        if (user is null || !user.IsActive)
            return Result.Failure<AuthenticatedUserDto>(genericFailure);

        if (!_passwordHasher.Verify(user.PasswordHash, command.Password))
            return Result.Failure<AuthenticatedUserDto>(genericFailure);

        user.RecordLogin();
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(new AuthenticatedUserDto(
            user.Id,
            user.Username,
            user.Email,
            user.FullName,
            user.Role,
            user.DepartmentId,
            user.MustChangePassword,
            user.IsFinanceApprover));
    }
}
