using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Errors;

namespace SureBudgetRequest.Application.Vendors.Commands.SetDefaultVendorPaymentAccount;

public sealed record SetDefaultVendorPaymentAccountCommand(
    Guid VendorId,
    Guid PaymentAccountId) : IRequest<Result>;

public sealed class SetDefaultVendorPaymentAccountCommandHandler
    : IRequestHandler<SetDefaultVendorPaymentAccountCommand, Result>
{
    private readonly IVendorRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public SetDefaultVendorPaymentAccountCommandHandler(IVendorRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(SetDefaultVendorPaymentAccountCommand command, CancellationToken ct)
    {
        var vendor = await _repository.GetByIdAsync(command.VendorId, ct);
        if (vendor is null) return Result.Failure(VendorErrors.GenericNotFound);

        try
        {
            vendor.SetDefaultPaymentAccount(command.PaymentAccountId);
        }
        catch (Exception ex)
        {
            return Result.Failure(VendorErrors.ValidationError(ex.Message));
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
