using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Errors;

namespace SureBudgetRequest.Application.AccountingCategories.Commands.UpdateAccountingCategory;

public sealed record UpdateAccountingCategoryCommand(Guid Id, string Name) : IRequest<Result>;

public sealed class UpdateAccountingCategoryCommandHandler
    : IRequestHandler<UpdateAccountingCategoryCommand, Result>
{
    private readonly IAccountingCategoryRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateAccountingCategoryCommandHandler(IAccountingCategoryRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(UpdateAccountingCategoryCommand command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
            return Result.Failure(AccountingCategoryErrors.ValidationError("Accounting category name is required."));

        var category = await _repository.GetByIdAsync(command.Id, ct);
        if (category is null)
            return Result.Failure(AccountingCategoryErrors.GenericNotFound);

        var existing = await _repository.GetByNameAsync(command.Name, ct);
        if (existing is not null && existing.Id != command.Id)
            return Result.Failure(AccountingCategoryErrors.AlreadyExists(existing.Name));

        try
        {
            category.Rename(command.Name);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure(AccountingCategoryErrors.ValidationError(ex.Message));
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
