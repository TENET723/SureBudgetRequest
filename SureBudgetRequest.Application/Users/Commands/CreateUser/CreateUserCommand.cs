using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.Abstractions.Security;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;

namespace SureBudgetRequest.Application.Users.Commands.CreateUser;

public sealed record CreateUserCommand(
    string Username,
    string FullName,
    string Email,
    string InitialPassword,
    Guid DepartmentId,
    UserRole Role,
    string? SlackUserId,
    bool IsFinanceApprover = false) : IRequest<Result<Guid>>;

public sealed class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, Result<Guid>>
{
    public const int MinimumPasswordLength = 10;

    private readonly IUserRepository _userRepository;
    private readonly IDepartmentRepository _departmentRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;

    public CreateUserCommandHandler(
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

    public async Task<Result<Guid>> Handle(CreateUserCommand command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.Email))
            return Result.Failure<Guid>("Email is required.");

        if (string.IsNullOrEmpty(command.InitialPassword)
            || command.InitialPassword.Length < MinimumPasswordLength)
            return Result.Failure<Guid>($"Initial password must be at least {MinimumPasswordLength} characters.");

        // The Finance-approver flag only makes sense for Finance users.
        // Reject up front rather than silently dropping the flag.
        if (command.IsFinanceApprover && command.Role != UserRole.Finance)
            return Result.Failure<Guid>("Only a user with the Finance role can be marked as a Finance Approver.");

        if (await _userRepository.EmailExistsAsync(command.Email, ct))
            return Result.Failure<Guid>("A user with this email already exists.");

        var dept = await _departmentRepository.GetByIdAsync(command.DepartmentId, ct);
        if (dept is null)
            return Result.Failure<Guid>("Department not found.");

        User user;
        try
        {
            user = new User(command.Username, command.FullName, command.Email, command.DepartmentId, command.Role);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<Guid>(ex.Message);
        }

        // MustChangePassword defaults to true in the User constructor.
        user.SetPasswordHash(_passwordHasher.Hash(command.InitialPassword), mustChangeOnNextLogin: true);
        user.SetSlackUserId(command.SlackUserId);

        if (command.IsFinanceApprover)
            user.SetFinanceApprover(true);

        await _userRepository.AddAsync(user, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success(user.Id);
    }
}
