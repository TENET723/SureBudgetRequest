using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Errors;

namespace SureBudgetRequest.Application.AccountingCategories.Commands.CreateAccountingCategory;

public sealed record CreateAccountingCategoryCommand(string Name) : IRequest<Result<Guid>>;

public sealed class CreateAccountingCategoryCommandHandler
    : IRequestHandler<CreateAccountingCategoryCommand, Result<Guid>>
{
    private readonly IAccountingCategoryRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateAccountingCategoryCommandHandler(IAccountingCategoryRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Guid>> Handle(CreateAccountingCategoryCommand command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
            return Result.Failure<Guid>(AccountingCategoryErrors.ValidationError("Accounting category name is required."));

        var existing = await _repository.GetByNameAsync(command.Name, ct);
        if (existing is not null)
            return Result.Failure<Guid>(AccountingCategoryErrors.AlreadyExists(existing.Name));

        AccountingCategory category;
        try
        {
            category = new AccountingCategory(command.Name);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<Guid>(AccountingCategoryErrors.ValidationError(ex.Message));
        }

        await _repository.AddAsync(category, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success(category.Id);
    }
}
