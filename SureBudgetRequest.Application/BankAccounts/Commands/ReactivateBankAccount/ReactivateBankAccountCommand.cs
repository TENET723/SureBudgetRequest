using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Errors;

namespace SureBudgetRequest.Application.BankAccounts.Commands.ReactivateBankAccount;

public sealed record ReactivateBankAccountCommand(Guid BankAccountId) : IRequest<Result>;

public sealed class ReactivateBankAccountCommandHandler
    : IRequestHandler<ReactivateBankAccountCommand, Result>
{
    private readonly IBankAccountRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public ReactivateBankAccountCommandHandler(IBankAccountRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(ReactivateBankAccountCommand command, CancellationToken ct)
    {
        var account = await _repository.GetByIdAsync(command.BankAccountId, ct);
        if (account is null) return Result.Failure(BankAccountErrors.GenericNotFound);

        account.Reactivate();
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
