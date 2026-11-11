using MediatR;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Errors;

namespace SureBudgetRequest.Application.BudgetRequests.Queries.GetBudgetRequest;

public sealed record GetBudgetRequestQuery(Guid BudgetRequestId) : IRequest<Result<BudgetRequestDto>>;

public sealed class GetBudgetRequestQueryHandler
    : IRequestHandler<GetBudgetRequestQuery, Result<BudgetRequestDto>>
{
    private readonly IBudgetRequestRepository _repository;
    private readonly ICoaRepository _coaRepository;
    private readonly IWithdrawMethodRepository _withdrawMethodRepository;
    private readonly IBudgetCategoryRepository _budgetCategoryRepository;
    private readonly IVendorRepository _vendorRepository;

    public GetBudgetRequestQueryHandler(
        IBudgetRequestRepository repository,
        ICoaRepository coaRepository,
        IWithdrawMethodRepository withdrawMethodRepository,
        IBudgetCategoryRepository budgetCategoryRepository,
        IVendorRepository vendorRepository)
    {
        _repository = repository;
        _coaRepository = coaRepository;
        _withdrawMethodRepository = withdrawMethodRepository;
        _budgetCategoryRepository = budgetCategoryRepository;
        _vendorRepository = vendorRepository;
    }

    public async Task<Result<BudgetRequestDto>> Handle(
        GetBudgetRequestQuery request,
        CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.BudgetRequestId, cancellationToken);
        if (entity is null)
            return Result.Failure<BudgetRequestDto>(BudgetRequestErrors.NotFound(request.BudgetRequestId));

        // Resolve the assigned Coa for display (null when never approved or when
        // the row pre-dates the COA feature).
        var coa = entity.CoaId.HasValue
            ? await _coaRepository.GetByIdAsync(entity.CoaId.Value, cancellationToken)
            : null;

        // Resolve the chosen withdraw method (null only for rows that pre-date
        // the feature).
        var withdrawMethod = entity.WithdrawMethodId.HasValue
            ? await _withdrawMethodRepository.GetByIdAsync(entity.WithdrawMethodId.Value, cancellationToken)
            : null;

        // Resolve the chosen budget category for display (null when unset).
        var budgetCategory = entity.BudgetCategoryId.HasValue
            ? await _budgetCategoryRepository.GetByIdAsync(entity.BudgetCategoryId.Value, cancellationToken)
            : null;

        // Resolve vendor for display (null when unset).
        Vendor? vendor = null;
        if (entity.VendorId.HasValue)
        {
            vendor = await _vendorRepository.GetByIdAsync(entity.VendorId.Value, cancellationToken);
        }

        return Result.Success(BudgetRequestDto.FromEntity(entity, coa, withdrawMethod, budgetCategory, vendor));
    }
}
