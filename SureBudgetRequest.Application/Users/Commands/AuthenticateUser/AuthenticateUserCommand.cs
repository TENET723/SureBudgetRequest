using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.Abstractions.Security;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;

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
    bool IsFinanceApprover,
    bool IsDepartmentHead);

public sealed class AuthenticateUserCommandHandler
    : IRequestHandler<AuthenticateUserCommand, Result<AuthenticatedUserDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly IDepartmentRepository _departmentRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;

    public AuthenticateUserCommandHandler(
        IUserRepository userRepository,
        IDepartmentRepository departmentRepository,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _departmentRepository = departmentRepository;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<AuthenticatedUserDto>> Handle(
        AuthenticateUserCommand command,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.Email) || string.IsNullOrWhiteSpace(command.Password))
            return Result.Failure<AuthenticatedUserDto>(UserErrors.InvalidCredentials);

        var user = await _userRepository.GetByEmailAsync(command.Email, ct);
        if (user is null || !user.IsActive)
            return Result.Failure<AuthenticatedUserDto>(UserErrors.InvalidCredentials);

        if (!_passwordHasher.Verify(user.PasswordHash, command.Password))
            return Result.Failure<AuthenticatedUserDto>(UserErrors.InvalidCredentials);

        user.RecordLogin();
        await _unitOfWork.SaveChangesAsync(ct);

        var activeDepts = await _departmentRepository.ListAsync(includeInactive: false, ct);
        var isDepartmentHead = activeDepts.Any(d => d.HeadUserId == user.Id);

        return Result.Success(new AuthenticatedUserDto(
            user.Id,
            user.Username,
            user.Email,
            user.FullName,
            user.Role,
            user.DepartmentId,
            user.MustChangePassword,
            user.IsFinanceApprover,
            isDepartmentHead));
    }
}
