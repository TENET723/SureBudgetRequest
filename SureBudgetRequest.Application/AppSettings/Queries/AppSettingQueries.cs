using MediatR;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Entities;

namespace SureBudgetRequest.Application.AppSettings.Queries;

public sealed record AppSettingDto(
    string Key,
    string Value,
    string? Description)
{
    public static AppSettingDto FromEntity(AppSetting s) =>
        new(s.Key, s.Value, s.Description);
}

// ── List ───────────────────────────────────────────────────────────────────

public sealed record ListAppSettingsQuery : IRequest<Result<IReadOnlyList<AppSettingDto>>>;

public sealed class ListAppSettingsQueryHandler
    : IRequestHandler<ListAppSettingsQuery, Result<IReadOnlyList<AppSettingDto>>>
{
    private readonly IAppSettingRepository _repository;
    public ListAppSettingsQueryHandler(IAppSettingRepository repository) => _repository = repository;

    public async Task<Result<IReadOnlyList<AppSettingDto>>> Handle(ListAppSettingsQuery request, CancellationToken ct)
    {
        var items = await _repository.ListAsync(ct);
        return Result.Success<IReadOnlyList<AppSettingDto>>(items.Select(AppSettingDto.FromEntity).ToList());
    }
}
