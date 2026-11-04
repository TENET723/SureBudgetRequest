using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.Users.Queries;
using SureBudgetRequest.Domain.Common;

namespace SureBudgetRequest.Application.Users.Queries.GetAvailableDelegations;

public sealed record GetAvailableDelegationsQuery() : IRequest<Result<IReadOnlyList<UserDto>>>;

public sealed class GetAvailableDelegationsQueryHandler
    : IRequestHandler<GetAvailableDelegationsQuery, Result<IReadOnlyList<UserDto>>>
{
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUser _currentUser;

    public GetAvailableDelegationsQueryHandler(
        IUserRepository userRepository,
        ICurrentUser currentUser)
    {
        _userRepository = userRepository;
        _currentUser = currentUser;
    }

    public async Task<Result<IReadOnlyList<UserDto>>> Handle(
        GetAvailableDelegationsQuery request,
        CancellationToken ct)
    {
        var targetUserId = _currentUser.RealActorUserId != Guid.Empty
            ? _currentUser.RealActorUserId
            : _currentUser.UserId;

        if (targetUserId == Guid.Empty)
            return Result.Success<IReadOnlyList<UserDto>>(Array.Empty<UserDto>());

        var users = await _userRepository.GetUsersOnLeaveDelegatedToAsync(targetUserId, ct);
        return Result.Success<IReadOnlyList<UserDto>>(users.Select(UserDto.FromEntity).ToList());
    }
}
