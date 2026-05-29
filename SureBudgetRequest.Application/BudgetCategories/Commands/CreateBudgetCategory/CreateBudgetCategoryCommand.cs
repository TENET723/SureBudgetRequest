using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Entities;

namespace SureBudgetRequest.Application.BudgetCategories.Commands.CreateBudgetCategory;

public sealed record CreateBudgetCategoryCommand(string Name) : IRequest<Result<Guid>>;

public sealed class CreateBudgetCategoryCommandHandler
    : IRequestHandler<CreateBudgetCategoryCommand, Result<Guid>>
{
    private readonly IBudgetCategoryRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateBudgetCategoryCommandHandler(IBudgetCategoryRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Guid>> Handle(CreateBudgetCategoryCommand command, CancellationToken ct)
    {
        var existing = await _repository.GetByNameAsync(command.Name, ct);
        if (existing is not null)
            return Result.Failure<Guid>($"Budget category '{existing.Name}' already exists.");

        BudgetCategory category;
        try
        {
            category = new BudgetCategory(command.Name);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<Guid>(ex.Message);
        }

        await _repository.AddAsync(category, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success(category.Id);
    }
}
