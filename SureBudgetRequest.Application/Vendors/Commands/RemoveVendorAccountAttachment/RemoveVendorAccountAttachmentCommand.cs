using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.Abstractions.Services;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Errors;

namespace SureBudgetRequest.Application.Vendors.Commands.RemoveVendorAccountAttachment;

public sealed record RemoveVendorAccountAttachmentCommand(
    Guid VendorId,
    Guid PaymentAccountId) : IRequest<Result>;

public sealed class RemoveVendorAccountAttachmentCommandHandler
    : IRequestHandler<RemoveVendorAccountAttachmentCommand, Result>
{
    private readonly IVendorRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFileStorage _fileStorage;

    public RemoveVendorAccountAttachmentCommandHandler(
        IVendorRepository repository,
        IUnitOfWork unitOfWork,
        IFileStorage fileStorage)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _fileStorage = fileStorage;
    }

    public async Task<Result> Handle(RemoveVendorAccountAttachmentCommand command, CancellationToken ct)
    {
        var vendor = await _repository.GetByIdAsync(command.VendorId, ct);
        if (vendor is null) return Result.Failure(VendorErrors.GenericNotFound);

        var account = vendor.PaymentAccounts.FirstOrDefault(a => a.Id == command.PaymentAccountId);
        if (account is null) return Result.Failure(VendorErrors.AccountNotFound);

        var oldStoredPath = account.AttachmentStoredPath;

        vendor.RemoveAccountAttachment(command.PaymentAccountId);

        await _unitOfWork.SaveChangesAsync(ct);

        if (!string.IsNullOrEmpty(oldStoredPath))
        {
            await _fileStorage.DeleteAsync(oldStoredPath, ct);
        }

        return Result.Success();
    }
}
