using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Errors;

namespace SureBudgetRequest.Application.Vendors.Commands.UpdateVendor;

public sealed record UpdateVendorCommand(
    Guid VendorId,
    string Name,
    string? ContactPerson = null,
    string? Phone = null,
    string? Email = null,
    string? Address = null) : IRequest<Result>;

public sealed class UpdateVendorCommandHandler : IRequestHandler<UpdateVendorCommand, Result>
{
    private readonly IVendorRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateVendorCommandHandler(IVendorRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(UpdateVendorCommand command, CancellationToken ct)
    {
        var vendor = await _repository.GetByIdAsync(command.VendorId, ct);
        if (vendor is null) return Result.Failure(VendorErrors.GenericNotFound);

        var existing = await _repository.GetByNameAsync(command.Name, ct);
        if (existing is not null && existing.Id != command.VendorId)
            return Result.Failure(VendorErrors.AlreadyExists(command.Name.Trim()));

        try
        {
            vendor.UpdateDetails(
                command.Name,
                command.ContactPerson,
                command.Phone,
                command.Email,
                command.Address);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure(VendorErrors.ValidationError(ex.Message));
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
