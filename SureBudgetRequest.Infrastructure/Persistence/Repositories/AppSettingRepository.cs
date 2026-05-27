using Microsoft.EntityFrameworkCore;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Entities;

namespace SureBudgetRequest.Infrastructure.Persistence.Repositories;

public sealed class AppSettingRepository : IAppSettingRepository
{
    private readonly AppDbContext _context;

    public AppSettingRepository(AppDbContext context) => _context = context;

    public async Task<AppSetting?> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
        => await _context.AppSettings.FindAsync([key], cancellationToken);

    public async Task<IReadOnlyList<AppSetting>> ListAsync(CancellationToken cancellationToken = default)
        => await _context.AppSettings.OrderBy(s => s.Key).ToListAsync(cancellationToken);

    public async Task AddAsync(AppSetting setting, CancellationToken cancellationToken = default)
        => await _context.AppSettings.AddAsync(setting, cancellationToken);
}
