using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Errors;

namespace SureBudgetRequest.Application.BudgetCategories.Commands.ReactivateBudgetCategory;

public sealed record ReactivateBudgetCategoryCommand(Guid BudgetCategoryId) : IRequest<Result>;

public sealed class ReactivateBudgetCategoryCommandHandler
    : IRequestHandler<ReactivateBudgetCategoryCommand, Result>
{
    private readonly IBudgetCategoryRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public ReactivateBudgetCategoryCommandHandler(IBudgetCategoryRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(ReactivateBudgetCategoryCommand command, CancellationToken ct)
    {
        var category = await _repository.GetByIdAsync(command.BudgetCategoryId, ct);
        if (category is null) return Result.Failure(BudgetCategoryErrors.GenericNotFound);

        category.Reactivate();
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
