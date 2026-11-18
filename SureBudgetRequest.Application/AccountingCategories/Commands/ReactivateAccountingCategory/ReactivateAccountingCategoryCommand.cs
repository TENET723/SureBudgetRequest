using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Errors;

namespace SureBudgetRequest.Application.AccountingCategories.Commands.ReactivateAccountingCategory;

public sealed record ReactivateAccountingCategoryCommand(Guid Id) : IRequest<Result>;

public sealed class ReactivateAccountingCategoryCommandHandler
    : IRequestHandler<ReactivateAccountingCategoryCommand, Result>
{
    private readonly IAccountingCategoryRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public ReactivateAccountingCategoryCommandHandler(IAccountingCategoryRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(ReactivateAccountingCategoryCommand command, CancellationToken ct)
    {
        var category = await _repository.GetByIdAsync(command.Id, ct);
        if (category is null)
            return Result.Failure(AccountingCategoryErrors.GenericNotFound);

        category.Reactivate();
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
