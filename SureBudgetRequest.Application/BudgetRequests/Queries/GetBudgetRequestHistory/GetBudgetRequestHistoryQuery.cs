using MediatR;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;

namespace SureBudgetRequest.Application.BudgetRequests.Queries.GetBudgetRequestHistory;

public sealed record BudgetRequestModificationDto(
    Guid Id,
    Guid BudgetRequestId,
    Guid ModifiedByUserId,
    string ModifiedByUserName,
    DateTime ModifiedAt);

public sealed record GetBudgetRequestHistoryQuery(Guid BudgetRequestId) : IRequest<Result<IReadOnlyList<BudgetRequestModificationDto>>>;

public sealed class GetBudgetRequestHistoryQueryHandler
    : IRequestHandler<GetBudgetRequestHistoryQuery, Result<IReadOnlyList<BudgetRequestModificationDto>>>
{
    private readonly IBudgetRequestModificationRepository _repository;
    private readonly IUserRepository _userRepository;

    public GetBudgetRequestHistoryQueryHandler(
        IBudgetRequestModificationRepository repository,
        IUserRepository userRepository)
    {
        _repository = repository;
        _userRepository = userRepository;
    }

    public async Task<Result<IReadOnlyList<BudgetRequestModificationDto>>> Handle(
        GetBudgetRequestHistoryQuery request,
        CancellationToken cancellationToken)
    {
        var logs = await _repository.ListByRequestIdAsync(request.BudgetRequestId, cancellationToken);
        
        var dtos = new List<BudgetRequestModificationDto>();
        var userCache = new Dictionary<Guid, string>();

        foreach (var log in logs)
        {
            if (!userCache.TryGetValue(log.ModifiedByUserId, out var userName))
            {
                var user = await _userRepository.GetByIdAsync(log.ModifiedByUserId, cancellationToken);
                userName = user?.FullName ?? "Unknown User";
                userCache[log.ModifiedByUserId] = userName;
            }

            dtos.Add(new BudgetRequestModificationDto(
                log.Id,
                log.BudgetRequestId,
                log.ModifiedByUserId,
                userName,
                log.ModifiedAt));
        }

        return Result.Success<IReadOnlyList<BudgetRequestModificationDto>>(dtos);
    }
}
