using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Errors;

namespace SureBudgetRequest.Application.BankAccounts.Commands.UpdateBankAccount;

public sealed record UpdateBankAccountCommand(
    Guid BankAccountId,
    string BankName,
    string? AccountNumber,
    string? AccountHolderName,
    bool IsActive) : IRequest<Result>;

public sealed class UpdateBankAccountCommandHandler
    : IRequestHandler<UpdateBankAccountCommand, Result>
{
    private readonly IBankAccountRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateBankAccountCommandHandler(IBankAccountRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(UpdateBankAccountCommand command, CancellationToken ct)
    {
        var account = await _repository.GetByIdAsync(command.BankAccountId, ct);
        if (account is null) return Result.Failure(BankAccountErrors.GenericNotFound);

        try
        {
            account.Update(command.BankName, command.AccountNumber, command.AccountHolderName);
            if (command.IsActive) account.Reactivate(); else account.Deactivate();
        }
        catch (ArgumentException ex)
        {
            return Result.Failure(BankAccountErrors.ValidationError(ex.Message));
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
