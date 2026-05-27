using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Entities;

namespace SureBudgetRequest.Infrastructure.Persistence.Repositories;

public sealed class BudgetRequestModificationRepository : IBudgetRequestModificationRepository
{
    private readonly AppDbContext _context;

    public BudgetRequestModificationRepository(AppDbContext context) => _context = context;

    public async Task AddAsync(BudgetRequestModification modification, CancellationToken cancellationToken = default)
    {
        await _context.Set<BudgetRequestModification>().AddAsync(modification, cancellationToken);
    }
}
