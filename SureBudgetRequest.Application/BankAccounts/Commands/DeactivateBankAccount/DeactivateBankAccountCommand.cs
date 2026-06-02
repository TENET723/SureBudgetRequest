using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Errors;

namespace SureBudgetRequest.Application.BankAccounts.Commands.DeactivateBankAccount;

public sealed record DeactivateBankAccountCommand(Guid BankAccountId) : IRequest<Result>;

public sealed class DeactivateBankAccountCommandHandler
    : IRequestHandler<DeactivateBankAccountCommand, Result>
{
    private readonly IBankAccountRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public DeactivateBankAccountCommandHandler(IBankAccountRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(DeactivateBankAccountCommand command, CancellationToken ct)
    {
        var account = await _repository.GetByIdAsync(command.BankAccountId, ct);
        if (account is null) return Result.Failure(BankAccountErrors.GenericNotFound);

        account.Deactivate();
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
