using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Errors;

namespace SureBudgetRequest.Application.Vendors.Commands.DeactivateVendor;

public sealed record DeactivateVendorCommand(Guid VendorId) : IRequest<Result>;

public sealed class DeactivateVendorCommandHandler : IRequestHandler<DeactivateVendorCommand, Result>
{
    private readonly IVendorRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public DeactivateVendorCommandHandler(IVendorRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(DeactivateVendorCommand command, CancellationToken ct)
    {
        var vendor = await _repository.GetByIdAsync(command.VendorId, ct);
        if (vendor is null) return Result.Failure(VendorErrors.GenericNotFound);

        vendor.Deactivate();
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
