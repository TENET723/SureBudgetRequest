using MediatR;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.BudgetRequests.Queries.ListBudgetRequests;
using SureBudgetRequest.Application.Common;
using SureBudgetRequest.Application.Tags.Models;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;

namespace SureBudgetRequest.Application.BudgetRequests.Queries.SearchBudgetRequests;

/// <summary>
/// Paged, sorted, server-side search over budget requests. Carries the same
/// filter dimensions as <see cref="ListBudgetRequestsQuery"/> plus a free-text
/// <see cref="SearchTerm"/>, sort selection, and paging. Returns one page of
/// results wrapped in a <see cref="PagedResult{T}"/> with the total matching
/// count.
///
/// As with the list query, the caller owns role-based scoping (e.g. forcing
/// <see cref="DepartmentId"/> or <see cref="RequesterId"/>).
/// </summary>
public sealed record SearchBudgetRequestsQuery(
    Guid? RequesterId = null,
    Guid? DepartmentId = null,
    RequestStatus? Status = null,
    IReadOnlyCollection<RequestStatus>? Statuses = null,
    DateTime? SubmittedFromUtc = null,
    DateTime? SubmittedUntilUtc = null,
    Guid? CoaId = null,
    string? CurrencyCode = null,
    Guid? ApproverId = null,
    bool? OverLimitOnly = null,
    IReadOnlyCollection<BudgetRequestType>? Types = null,
    decimal? AmountInMmkFrom = null,
    decimal? AmountInMmkTo = null,
    bool? PeriodOverrunOnly = null,
    string? SearchTerm = null,
    bool? OverdueAdvanceOnly = null,
    BudgetRequestSortBy SortBy = BudgetRequestSortBy.SubmittedAt,
    bool SortDescending = true,
    int Page = 1,
    int PageSize = 20,
    Guid? WithdrawMethodId = null,
    Guid? TagId = null,
    Guid? ViewerUserId = null) : IRequest<Result<PagedResult<BudgetRequestSummaryDto>>>;

public sealed class SearchBudgetRequestsQueryHandler
    : IRequestHandler<SearchBudgetRequestsQuery, Result<PagedResult<BudgetRequestSummaryDto>>>
{
    private readonly IBudgetRequestRepository _repository;
    private readonly ICoaRepository _coaRepository;
    private readonly IWithdrawMethodRepository _withdrawMethodRepository;
    private readonly IUserRepository? _userRepository;
    private readonly IAppSettingRepository? _appSettingRepository;

    public SearchBudgetRequestsQueryHandler(
        IBudgetRequestRepository repository,
        ICoaRepository coaRepository,
        IWithdrawMethodRepository withdrawMethodRepository,
        IUserRepository? userRepository = null,
        IAppSettingRepository? appSettingRepository = null)
    {
        _repository = repository;
        _coaRepository = coaRepository;
        _withdrawMethodRepository = withdrawMethodRepository;
        _userRepository = userRepository;
        _appSettingRepository = appSettingRepository;
    }

    public async Task<Result<PagedResult<BudgetRequestSummaryDto>>> Handle(
        SearchBudgetRequestsQuery request,
        CancellationToken cancellationToken)
    {
        var (entities, totalCount) = await _repository.SearchAsync(
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
            request.Types,
            request.AmountInMmkFrom,
            request.AmountInMmkTo,
            request.PeriodOverrunOnly,
            request.SearchTerm,
            request.OverdueAdvanceOnly,
            request.SortBy,
            request.SortDescending,
            request.Page,
            request.PageSize,
            request.WithdrawMethodId,
            request.TagId,
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
            // name.
            var allCoas = await _coaRepository.ListAsync(includeInactive: true, cancellationToken);
            coaLookup = allCoas
                .Where(c => coaIds.Contains(c.Id))
                .ToDictionary(c => c.Id);
        }

        // Same batch-lookup pattern for withdraw methods.
        var methodIds = entities
            .Select(e => e.WithdrawMethodId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToHashSet();

        Dictionary<Guid, WithdrawMethod> methodLookup;
        if (methodIds.Count == 0)
        {
            methodLookup = new Dictionary<Guid, WithdrawMethod>();
        }
        else
        {
            var allMethods = await _withdrawMethodRepository.ListAsync(includeInactive: true, cancellationToken);
            methodLookup = allMethods
                .Where(m => methodIds.Contains(m.Id))
                .ToDictionary(m => m.Id);
        }

        User? viewer = null;
        if (request.ViewerUserId.HasValue && _userRepository is not null)
        {
            viewer = await _userRepository.GetByIdAsync(request.ViewerUserId.Value, cancellationToken);
        }

        var dtos = entities
            .Select(e =>
            {
                Coa? coa = null;
                if (e.CoaId.HasValue)
                    coaLookup.TryGetValue(e.CoaId.Value, out coa);

                WithdrawMethod? method = null;
                if (e.WithdrawMethodId.HasValue)
                    methodLookup.TryGetValue(e.WithdrawMethodId.Value, out method);

                List<RequestTagSummaryDto>? tagDtos = null;
                if (e.Tags.Count > 0)
                {
                    IEnumerable<BudgetRequestTag> visibleTags;
                    if (viewer is null || viewer.Role == UserRole.Admin)
                    {
                        visibleTags = e.Tags.Where(t => t.Tag is not null && t.Tag.IsActive);
                    }
                    else
                    {
                        // Department-isolated: in list/report views, users only see tags belonging to their own department.
                        // Cross-department tags (Requester, Finance, Accounting, Management) are exclusively visible on the Request Detail page.
                        visibleTags = e.Tags.Where(t =>
                            t.Tag is not null &&
                            t.Tag.IsActive &&
                            viewer.DepartmentId != Guid.Empty &&
                            t.TagDepartmentId == viewer.DepartmentId);
                    }

                    tagDtos = visibleTags
                        .Select(t => new RequestTagSummaryDto(t.TagId, t.Tag!.Name, t.Tag.ColorHex, t.Tag.FontColorHex, t.TagDepartmentId))
                        .ToList();
                }

                return BudgetRequestSummaryDto.FromEntity(e, coa, method, tagDtos);
            })
            .ToList();

        var paged = new PagedResult<BudgetRequestSummaryDto>(
            dtos, totalCount, request.Page, request.PageSize);

        return Result.Success(paged);
    }
}
