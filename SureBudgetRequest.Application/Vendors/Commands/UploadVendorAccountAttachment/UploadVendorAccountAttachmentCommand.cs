using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.Abstractions.Services;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Errors;

namespace SureBudgetRequest.Application.Vendors.Commands.UploadVendorAccountAttachment;

public sealed record UploadVendorAccountAttachmentCommand(
    Guid VendorId,
    Guid PaymentAccountId,
    string FileName,
    string ContentType,
    long SizeBytes,
    string PreUploadedPath) : IRequest<Result>;

public sealed class UploadVendorAccountAttachmentCommandHandler
    : IRequestHandler<UploadVendorAccountAttachmentCommand, Result>
{
    private readonly IVendorRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFileStorage _fileStorage;

    public UploadVendorAccountAttachmentCommandHandler(
        IVendorRepository repository,
        IUnitOfWork unitOfWork,
        IFileStorage fileStorage)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _fileStorage = fileStorage;
    }

    public async Task<Result> Handle(UploadVendorAccountAttachmentCommand command, CancellationToken ct)
    {
        var vendor = await _repository.GetByIdAsync(command.VendorId, ct);
        if (vendor is null) return Result.Failure(VendorErrors.GenericNotFound);

        var account = vendor.PaymentAccounts.FirstOrDefault(a => a.Id == command.PaymentAccountId);
        if (account is null) return Result.Failure(VendorErrors.AccountNotFound);

        var oldStoredPath = account.AttachmentStoredPath;

        var ext = System.IO.Path.GetExtension(command.FileName).ToLowerInvariant();
        if (ext is not ".pdf" and not ".png" and not ".jpg" and not ".jpeg" and not ".webp")
        {
            ext = ".bin";
        }

        var newStoredPath = $"vendors/{vendor.Id}/{Guid.NewGuid():N}{ext}";
        try
        {
            await _fileStorage.MoveAsync(command.PreUploadedPath, newStoredPath, ct);
        }
        catch (Exception ex)
        {
            return Result.Failure($"Failed to store attachment: {ex.Message}");
        }

        vendor.SetAccountAttachment(
            command.PaymentAccountId,
            command.FileName,
            newStoredPath,
            command.ContentType,
            command.SizeBytes);

        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            await _fileStorage.DeleteAsync(newStoredPath, ct);
            return Result.Failure($"Database save failed: {ex.Message}");
        }

        if (!string.IsNullOrEmpty(oldStoredPath))
        {
            await _fileStorage.DeleteAsync(oldStoredPath, ct);
        }

        return Result.Success();
    }
}
