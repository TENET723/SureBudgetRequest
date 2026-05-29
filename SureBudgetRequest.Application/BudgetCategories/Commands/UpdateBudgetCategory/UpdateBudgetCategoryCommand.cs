using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Errors;

namespace SureBudgetRequest.Application.BudgetCategories.Commands.UpdateBudgetCategory;

public sealed record UpdateBudgetCategoryCommand(
    Guid BudgetCategoryId,
    string Name,
    bool IsActive) : IRequest<Result>;

public sealed class UpdateBudgetCategoryCommandHandler
    : IRequestHandler<UpdateBudgetCategoryCommand, Result>
{
    private readonly IBudgetCategoryRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateBudgetCategoryCommandHandler(IBudgetCategoryRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(UpdateBudgetCategoryCommand command, CancellationToken ct)
    {
        var category = await _repository.GetByIdAsync(command.BudgetCategoryId, ct);
        if (category is null) return Result.Failure(BudgetCategoryErrors.GenericNotFound);

        // If the name is being changed, ensure the new name isn't taken by another row.
        if (!string.Equals(category.Name, command.Name?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            var existing = await _repository.GetByNameAsync(command.Name ?? string.Empty, ct);
            if (existing is not null && existing.Id != category.Id)
                return Result.Failure(BudgetCategoryErrors.AlreadyExists(existing.Name));
        }

        try
        {
            category.Rename(command.Name);
            if (command.IsActive) category.Reactivate(); else category.Deactivate();
        }
        catch (ArgumentException ex)
        {
            return Result.Failure(BudgetCategoryErrors.ValidationError(ex.Message));
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
