using MediatR;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Enums;

namespace SureBudgetRequest.Application.Users.Queries;

// ── Get single user ──────────────────────────────────────────────────────────

public sealed record GetUserQuery(Guid UserId) : IRequest<Result<UserDto>>;

public sealed class GetUserQueryHandler : IRequestHandler<GetUserQuery, Result<UserDto>>
{
    private readonly IUserRepository _repository;
    public GetUserQueryHandler(IUserRepository repository) => _repository = repository;

    public async Task<Result<UserDto>> Handle(GetUserQuery request, CancellationToken ct)
    {
        var user = await _repository.GetByIdAsync(request.UserId, ct);
        return user is null
            ? Result.Failure<UserDto>("User not found.")
            : Result.Success(UserDto.FromEntity(user));
    }
}

// ── List users ────────────────────────────────────────────────────────────────

public sealed record ListUsersQuery(
    Guid? DepartmentId = null,
    UserRole? Role = null,
    bool IncludeInactive = false) : IRequest<Result<IReadOnlyList<UserDto>>>;

public sealed class ListUsersQueryHandler
    : IRequestHandler<ListUsersQuery, Result<IReadOnlyList<UserDto>>>
{
    private readonly IUserRepository _repository;
    public ListUsersQueryHandler(IUserRepository repository) => _repository = repository;

    public async Task<Result<IReadOnlyList<UserDto>>> Handle(ListUsersQuery request, CancellationToken ct)
    {
        // Use named arguments — IUserRepository.ListAsync gained an
        // `isFinanceApprover` parameter before `cancellationToken`, so positional
        // calls would now bind the ct value to the wrong slot.
        var users = await _repository.ListAsync(
            departmentId: request.DepartmentId,
            role: request.Role,
            includeInactive: request.IncludeInactive,
            cancellationToken: ct);
        return Result.Success<IReadOnlyList<UserDto>>(users.Select(UserDto.FromEntity).ToList());
    }
}
