using MediatR;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;

namespace SureBudgetRequest.Application.BudgetRequests.Queries.ListBudgetRequests;

/// <summary>
/// Lists budget requests with optional filtering. Used both by role-scoped
/// pages (Inbox, MyRequests, OutstandingPayments) and the report page at
/// <c>/reports/budget-requests</c>.
///
/// All parameters are optional and additive — any combination is valid. The
/// caller is responsible for role-based scoping (e.g. forcing
/// <see cref="DepartmentId"/> to the current Dept Head's department).
/// </summary>
/// <param name="SubmittedFromUtc">
/// Inclusive lower bound matched against <c>SubmittedAt</c>. Requests with
/// <c>SubmittedAt == null</c> (i.e. drafts) are excluded when either bound
/// is supplied.
/// </param>
/// <param name="SubmittedUntilUtc">
/// Exclusive upper bound matched against <c>SubmittedAt</c>. To include a
/// whole calendar day, callers should pass <c>nextDayMidnightUtc</c>.
/// </param>
/// <param name="OverLimitOnly">
/// <c>null</c> = no filter. <c>true</c> = only requests whose
/// <c>RequestedAmountInMmkAtSubmission &gt; DepartmentLimitAtSubmission</c>
/// (i.e. routed through Management). <c>false</c> = only within-limit
/// requests.
/// </param>
/// <param name="ApproverId">
/// Filters to requests where the given user appears in the approval chain
/// with an <c>Approved</c> or <c>AutoApproved</c> decision — at any stage
/// (Dept Head, Management, or Finance).
/// </param>
public sealed record ListBudgetRequestsQuery(
    Guid? RequesterId = null,
    Guid? DepartmentId = null,
    RequestStatus? Status = null,
    IReadOnlyCollection<RequestStatus>? Statuses = null,
    DateTime? SubmittedFromUtc = null,
    DateTime? SubmittedUntilUtc = null,
    Guid? CoaId = null,
    string? CurrencyCode = null,
    Guid? ApproverId = null,
    bool? OverLimitOnly = null) : IRequest<Result<IReadOnlyList<BudgetRequestSummaryDto>>>;

public sealed class ListBudgetRequestsQueryHandler
    : IRequestHandler<ListBudgetRequestsQuery, Result<IReadOnlyList<BudgetRequestSummaryDto>>>
{
    private readonly IBudgetRequestRepository _repository;
    private readonly ICoaRepository _coaRepository;

    public ListBudgetRequestsQueryHandler(
        IBudgetRequestRepository repository,
        ICoaRepository coaRepository)
    {
        _repository = repository;
        _coaRepository = coaRepository;
    }

    public async Task<Result<IReadOnlyList<BudgetRequestSummaryDto>>> Handle(
        ListBudgetRequestsQuery request,
        CancellationToken cancellationToken)
    {
        var entities = await _repository.ListAsync(
            request.RequesterId,
            request.DepartmentId,
            request.Status,
            request.Statuses,
            request.SubmittedFromUtc,
            request.SubmittedUntilUtc,
            request.CoaId,
            request.CurrencyCode,
            request.ApproverId,
            request.OverLimitOnly,
            cancellationToken);

        // Batch-resolve COA codes/names for display. The COA master list is
        // small (global, dozens of rows max), so a single ListAsync call is
        // cheaper than N round-trips and avoids any N+1 risk if EF lazy
        // loading is ever enabled.
        var coaIds = entities
            .Select(e => e.CoaId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToHashSet();

        Dictionary<Guid, Coa> coaLookup;
        if (coaIds.Count == 0)
        {
            coaLookup = new Dictionary<Guid, Coa>();
        }
        else
        {
            // includeInactive: true — historical requests may reference
            // since-deactivated codes; we still want to show their display
            // name in the report.
            var allCoas = await _coaRepository.ListAsync(includeInactive: true, cancellationToken);
            coaLookup = allCoas
                .Where(c => coaIds.Contains(c.Id))
                .ToDictionary(c => c.Id);
        }

        var dtos = entities
            .Select(e =>
            {
                Coa? coa = null;
                if (e.CoaId.HasValue)
                    coaLookup.TryGetValue(e.CoaId.Value, out coa);
                return BudgetRequestSummaryDto.FromEntity(e, coa);
            })
            .ToList();

        return Result.Success<IReadOnlyList<BudgetRequestSummaryDto>>(dtos);
    }
}
