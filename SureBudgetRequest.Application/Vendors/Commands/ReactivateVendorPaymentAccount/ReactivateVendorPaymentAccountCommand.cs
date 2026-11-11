using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Errors;

namespace SureBudgetRequest.Application.Vendors.Commands.ReactivateVendorPaymentAccount;

public sealed record ReactivateVendorPaymentAccountCommand(
    Guid VendorId,
    Guid PaymentAccountId) : IRequest<Result>;

public sealed class ReactivateVendorPaymentAccountCommandHandler
    : IRequestHandler<ReactivateVendorPaymentAccountCommand, Result>
{
    private readonly IVendorRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public ReactivateVendorPaymentAccountCommandHandler(IVendorRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(ReactivateVendorPaymentAccountCommand command, CancellationToken ct)
    {
        var vendor = await _repository.GetByIdAsync(command.VendorId, ct);
        if (vendor is null) return Result.Failure(VendorErrors.GenericNotFound);

        try
        {
            vendor.ReactivatePaymentAccount(command.PaymentAccountId);
        }
        catch (Exception ex)
        {
            return Result.Failure(VendorErrors.ValidationError(ex.Message));
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
