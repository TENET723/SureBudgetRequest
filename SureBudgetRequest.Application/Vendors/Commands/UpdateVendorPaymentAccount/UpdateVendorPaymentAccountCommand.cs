using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;

namespace SureBudgetRequest.Application.Vendors.Commands.UpdateVendorPaymentAccount;

public sealed record UpdateVendorPaymentAccountCommand(
    Guid VendorId,
    Guid PaymentAccountId,
    PaymentAccountType AccountType,
    string ProviderName,
    string AccountNumber,
    string AccountHolderName,
    string? BranchName = null) : IRequest<Result>;

public sealed class UpdateVendorPaymentAccountCommandHandler
    : IRequestHandler<UpdateVendorPaymentAccountCommand, Result>
{
    private readonly IVendorRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateVendorPaymentAccountCommandHandler(IVendorRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(UpdateVendorPaymentAccountCommand command, CancellationToken ct)
    {
        var vendor = await _repository.GetByIdAsync(command.VendorId, ct);
        if (vendor is null) return Result.Failure(VendorErrors.GenericNotFound);

        try
        {
            vendor.UpdatePaymentAccount(
                command.PaymentAccountId,
                command.AccountType,
                command.ProviderName,
                command.AccountNumber,
                command.AccountHolderName,
                command.BranchName);
        }
        catch (Exception ex)
        {
            return Result.Failure(VendorErrors.ValidationError(ex.Message));
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
