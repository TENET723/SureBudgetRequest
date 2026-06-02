using MediatR;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Entities;

namespace SureBudgetRequest.Application.BankAccounts.Queries;

public sealed record BankAccountDto(
    Guid Id,
    string BankName,
    string AccountNumber,
    string AccountHolderName,
    bool IsActive,
    DateTime CreatedAt)
{
    public static BankAccountDto FromEntity(BankAccount a) =>
        new(a.Id, a.BankName, a.AccountNumber, a.AccountHolderName, a.IsActive, a.CreatedAt);
}

// ── Get single ─────────────────────────────────────────────────────────────

public sealed record GetBankAccountQuery(Guid BankAccountId) : IRequest<Result<BankAccountDto>>;

public sealed class GetBankAccountQueryHandler : IRequestHandler<GetBankAccountQuery, Result<BankAccountDto>>
{
    private readonly IBankAccountRepository _repository;
    public GetBankAccountQueryHandler(IBankAccountRepository repository) => _repository = repository;

    public async Task<Result<BankAccountDto>> Handle(GetBankAccountQuery request, CancellationToken ct)
    {
        var account = await _repository.GetByIdAsync(request.BankAccountId, ct);
        return account is null
            ? Result.Failure<BankAccountDto>("Bank account not found.")
            : Result.Success(BankAccountDto.FromEntity(account));
    }
}

// ── List ───────────────────────────────────────────────────────────────────

public sealed record ListBankAccountsQuery(bool IncludeInactive = false)
    : IRequest<Result<IReadOnlyList<BankAccountDto>>>;

public sealed class ListBankAccountsQueryHandler
    : IRequestHandler<ListBankAccountsQuery, Result<IReadOnlyList<BankAccountDto>>>
{
    private readonly IBankAccountRepository _repository;
    public ListBankAccountsQueryHandler(IBankAccountRepository repository) => _repository = repository;

    public async Task<Result<IReadOnlyList<BankAccountDto>>> Handle(ListBankAccountsQuery request, CancellationToken ct)
    {
        var items = await _repository.ListAsync(request.IncludeInactive, ct);
        return Result.Success<IReadOnlyList<BankAccountDto>>(items.Select(BankAccountDto.FromEntity).ToList());
    }
}
