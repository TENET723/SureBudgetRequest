using MediatR;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.Common;
using SureBudgetRequest.Application.Tags.Models;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;

namespace SureBudgetRequest.Application.Tags.Queries;

public sealed record TagDto(
    Guid Id,
    Guid DepartmentId,
    string Name,
    string? ColorHex,
    string? FontColorHex,
    bool IsActive,
    Guid CreatedByUserId,
    DateTime CreatedAt)
{
    public string EffectiveFontColor => ColorContrastHelper.GetEffectiveFontColor(ColorHex, FontColorHex);

    public static TagDto FromEntity(Tag t) =>
        new(t.Id, t.DepartmentId, t.Name, t.ColorHex, t.FontColorHex, t.IsActive, t.CreatedByUserId, t.CreatedAt);
}

public sealed record RequestTagDto(
    Guid TagId,
    string Name,
    string? ColorHex,
    string? FontColorHex,
    Guid DepartmentId,
    string DepartmentName,
    Guid AttachedByUserId,
    DateTime AttachedAt,
    string TagGroup = "Other")
{
    public string EffectiveFontColor => ColorContrastHelper.GetEffectiveFontColor(ColorHex, FontColorHex);
}

// ── Get Single Tag ──────────────────────────────────────────────────────────

public sealed record GetTagQuery(Guid TagId) : IRequest<Result<TagDto>>;

public sealed class GetTagQueryHandler : IRequestHandler<GetTagQuery, Result<TagDto>>
{
    private readonly ITagRepository _tagRepository;

    public GetTagQueryHandler(ITagRepository tagRepository)
    {
        _tagRepository = tagRepository;
    }

    public async Task<Result<TagDto>> Handle(GetTagQuery request, CancellationToken ct)
    {
        var tag = await _tagRepository.GetByIdAsync(request.TagId, ct);
        return tag is null
            ? Result.Failure<TagDto>(TagErrors.NotFound)
            : Result.Success(TagDto.FromEntity(tag));
    }
}

// ── List Tags By Department ────────────────────────────────────────────────

public sealed record ListTagsByDepartmentQuery(Guid DepartmentId, bool IncludeInactive = false)
    : IRequest<Result<IReadOnlyList<TagDto>>>;

public sealed class ListTagsByDepartmentQueryHandler
    : IRequestHandler<ListTagsByDepartmentQuery, Result<IReadOnlyList<TagDto>>>
{
    private readonly ITagRepository _tagRepository;

    public ListTagsByDepartmentQueryHandler(ITagRepository tagRepository)
    {
        _tagRepository = tagRepository;
    }

    public async Task<Result<IReadOnlyList<TagDto>>> Handle(ListTagsByDepartmentQuery request, CancellationToken ct)
    {
        var tags = await _tagRepository.ListByDepartmentAsync(request.DepartmentId, request.IncludeInactive, ct);
        return Result.Success<IReadOnlyList<TagDto>>(tags.Select(TagDto.FromEntity).ToList());
    }
}

// ── Get Tags Attached to a Request (with Visibility Filtering) ──────────────

public sealed record GetRequestTagsQuery(Guid BudgetRequestId, Guid ViewerUserId)
    : IRequest<Result<IReadOnlyList<RequestTagDto>>>;

public sealed class GetRequestTagsQueryHandler
    : IRequestHandler<GetRequestTagsQuery, Result<IReadOnlyList<RequestTagDto>>>
{
    private readonly IBudgetRequestRepository _requestRepository;
    private readonly IUserRepository _userRepository;
    private readonly IDepartmentRepository _departmentRepository;
    private readonly IAppSettingRepository _appSettingRepository;

    public GetRequestTagsQueryHandler(
        IBudgetRequestRepository requestRepository,
        IUserRepository userRepository,
        IDepartmentRepository departmentRepository,
        IAppSettingRepository appSettingRepository)
    {
        _requestRepository = requestRepository;
        _userRepository = userRepository;
        _departmentRepository = departmentRepository;
        _appSettingRepository = appSettingRepository;
    }

    public async Task<Result<IReadOnlyList<RequestTagDto>>> Handle(GetRequestTagsQuery query, CancellationToken ct)
    {
        var request = await _requestRepository.GetByIdAsync(query.BudgetRequestId, ct);
        if (request is null)
            return Result.Failure<IReadOnlyList<RequestTagDto>>(BudgetRequestErrors.NotFound(query.BudgetRequestId));

        var viewer = await _userRepository.GetByIdAsync(query.ViewerUserId, ct);
        if (viewer is null || !viewer.IsActive)
            return Result.Failure<IReadOnlyList<RequestTagDto>>(UserErrors.NotFound(query.ViewerUserId));

        var requester = await _userRepository.GetByIdAsync(request.RequesterId, ct);

        // Load tag visibility configuration from AppSetting
        var configSetting = await _appSettingRepository.GetByKeyAsync(TagVisibilityConfig.SettingKey, ct);
        var config = TagVisibilityConfig.FromJson(configSetting?.Value);

        // Load department names for quick lookup and fallback mapping
        var allDepts = await _departmentRepository.ListAsync(includeInactive: true, ct);
        var deptDict = allDepts.ToDictionary(d => d.Id, d => d.Name);

        // Resolved Department IDs for Finance, Management, and Accounting (strictly from AppSetting, no fallback)
        var financeDeptId = config.FinanceDepartmentId;
        var managementDeptId = config.ManagementDepartmentId;
        var accountingDeptId = config.AccountingDepartmentId;

        // Visibility rules:
        // 1. Admin: sees all tags
        // 2. Own department: always sees own department's tags
        // 3. Otherwise: evaluate policy according to AppSetting RoleVisibilityMatrix
        var visibleTags = request.Tags.Where(t =>
        {
            if (viewer.Role == UserRole.Admin)
                return true;

            if (t.TagDepartmentId == viewer.DepartmentId)
                return true; // Always see own department's tags

            // Circuit breaker: If Finance and Management departments are not explicitly configured,
            // cross-department visibility is disabled. Only own-department tags and Admin can view.
            if (!config.IsConfigured)
                return false;

            // Determine viewer policy key
            string policyKey;
            bool isViewerRequesterDept = viewer.DepartmentId == request.DepartmentIdAtSubmission || viewer.Id == request.RequesterId;
            if (isViewerRequesterDept)
            {
                var reqRole = requester?.Role ?? UserRole.Employee;
                if (reqRole == UserRole.Finance)
                {
                    policyKey = "Finance";
                }
                else if (reqRole == UserRole.Accounting)
                {
                    policyKey = "Accounting";
                }
                else if (reqRole == UserRole.Management)
                {
                    policyKey = "Management";
                }
                else
                {
                    policyKey = "Requester";
                }
            }
            else
            {
                policyKey = viewer.Role.ToString();
            }

            if (!config.RoleVisibilityMatrix.TryGetValue(policyKey, out var allowedTypes) || allowedTypes is null || allowedTypes.Count == 0)
            {
                return false;
            }

            // Tag classification checks (independent checks to support overlap cases)
            bool isTagRequester = t.TagDepartmentId == request.DepartmentIdAtSubmission;
            bool isTagFinance = financeDeptId.HasValue && t.TagDepartmentId == financeDeptId.Value;
            bool isTagManagement = managementDeptId.HasValue && t.TagDepartmentId == managementDeptId.Value;
            bool isTagAccounting = accountingDeptId.HasValue && t.TagDepartmentId == accountingDeptId.Value;

            if (isTagRequester && allowedTypes.Any(a => string.Equals(a, "Requester", StringComparison.OrdinalIgnoreCase)))
                return true;

            if (isTagFinance && allowedTypes.Any(a => string.Equals(a, "Finance", StringComparison.OrdinalIgnoreCase)))
                return true;

            if (isTagManagement && allowedTypes.Any(a => string.Equals(a, "Management", StringComparison.OrdinalIgnoreCase)))
                return true;

            if (isTagAccounting && allowedTypes.Any(a => string.Equals(a, "Accounting", StringComparison.OrdinalIgnoreCase)))
                return true;

            return false;
        }).ToList();

        var result = visibleTags.Select(t =>
        {
            string group = "Other";
            if (t.TagDepartmentId == request.DepartmentIdAtSubmission)
            {
                group = "Requester";
            }
            else if (financeDeptId.HasValue && t.TagDepartmentId == financeDeptId.Value)
            {
                group = "Finance";
            }
            else if (accountingDeptId.HasValue && t.TagDepartmentId == accountingDeptId.Value)
            {
                group = "Accounting";
            }
            else if (managementDeptId.HasValue && t.TagDepartmentId == managementDeptId.Value)
            {
                group = "Management";
            }

            return new RequestTagDto(
                t.TagId,
                t.Tag?.Name ?? "Tag",
                t.Tag?.ColorHex,
                t.Tag?.FontColorHex,
                t.TagDepartmentId,
                deptDict.TryGetValue(t.TagDepartmentId, out var name) ? name : "Department",
                t.AttachedByUserId,
                t.AttachedAt,
                group);
        }).ToList();

        return Result.Success<IReadOnlyList<RequestTagDto>>(result);
    }
}

// ── List Visible Tags For User (For filter dropdowns) ───────────────────────

public sealed record VisibleTagDto(
    Guid Id,
    Guid DepartmentId,
    string DepartmentName,
    string Name,
    string? ColorHex,
    string? FontColorHex = null)
{
    public string EffectiveFontColor => ColorContrastHelper.GetEffectiveFontColor(ColorHex, FontColorHex);
    public string DisplayName => $"#{Name}";
    public string FullDisplayName => $"[{DepartmentName}] #{Name}";
}

public sealed record ListVisibleTagsForUserQuery(Guid UserId) : IRequest<Result<IReadOnlyList<VisibleTagDto>>>;

public sealed class ListVisibleTagsForUserQueryHandler
    : IRequestHandler<ListVisibleTagsForUserQuery, Result<IReadOnlyList<VisibleTagDto>>>
{
    private readonly ITagRepository _tagRepository;
    private readonly IUserRepository _userRepository;
    private readonly IDepartmentRepository _departmentRepository;

    public ListVisibleTagsForUserQueryHandler(
        ITagRepository tagRepository,
        IUserRepository userRepository,
        IDepartmentRepository departmentRepository)
    {
        _tagRepository = tagRepository;
        _userRepository = userRepository;
        _departmentRepository = departmentRepository;
    }

    public async Task<Result<IReadOnlyList<VisibleTagDto>>> Handle(ListVisibleTagsForUserQuery request, CancellationToken ct)
    {
        var viewer = await _userRepository.GetByIdAsync(request.UserId, ct);
        if (viewer is null || !viewer.IsActive)
            return Result.Failure<IReadOnlyList<VisibleTagDto>>(UserErrors.NotFound(request.UserId));

        var allDepts = await _departmentRepository.ListAsync(includeInactive: true, ct);
        var deptDict = allDepts.ToDictionary(d => d.Id, d => d.Name);

        IReadOnlyList<Tag> tags;
        if (viewer.Role == UserRole.Admin)
        {
            tags = await _tagRepository.ListAllActiveAsync(ct);
        }
        else
        {
            tags = await _tagRepository.ListByDepartmentAsync(viewer.DepartmentId, includeInactive: false, ct);
        }

        var result = tags.Select(t => new VisibleTagDto(
            t.Id,
            t.DepartmentId,
            deptDict.TryGetValue(t.DepartmentId, out var deptName) ? deptName : "Department",
            t.Name,
            t.ColorHex,
            t.FontColorHex)).ToList();

        return Result.Success<IReadOnlyList<VisibleTagDto>>(result);
    }
}
