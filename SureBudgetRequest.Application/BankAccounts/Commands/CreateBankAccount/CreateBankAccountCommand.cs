using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Errors;

namespace SureBudgetRequest.Application.BankAccounts.Commands.CreateBankAccount;

public sealed record CreateBankAccountCommand(
    string BankName,
    string? AccountNumber,
    string? AccountHolderName) : IRequest<Result<Guid>>;

public sealed class CreateBankAccountCommandHandler
    : IRequestHandler<CreateBankAccountCommand, Result<Guid>>
{
    private readonly IBankAccountRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateBankAccountCommandHandler(IBankAccountRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Guid>> Handle(CreateBankAccountCommand command, CancellationToken ct)
    {
        // Soft-check for an existing active account with the same number. There is
        // no DB unique index (account numbers can legitimately repeat once a row is
        // deactivated and replaced), so this is a best-effort guard.
        var number = (command.AccountNumber ?? string.Empty).Trim();
        if (!string.IsNullOrEmpty(number))
        {
            var existing = await _repository.ListAsync(includeInactive: false, ct);
            if (existing.Any(a => string.Equals(a.AccountNumber, number, StringComparison.OrdinalIgnoreCase)))
                return Result.Failure<Guid>(BankAccountErrors.AlreadyExists(number));
        }

        BankAccount account;
        try
        {
            account = new BankAccount(command.BankName, command.AccountNumber, command.AccountHolderName);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<Guid>(ex.Message);
        }

        await _repository.AddAsync(account, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success(account.Id);
    }
}
