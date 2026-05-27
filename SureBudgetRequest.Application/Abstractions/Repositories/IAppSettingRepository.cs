using SureBudgetRequest.Domain.Entities;

namespace SureBudgetRequest.Application.Abstractions.Repositories;

public interface IAppSettingRepository
{
    Task<AppSetting?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AppSetting>> ListAsync(CancellationToken cancellationToken = default);
    Task AddAsync(AppSetting setting, CancellationToken cancellationToken = default);
}
