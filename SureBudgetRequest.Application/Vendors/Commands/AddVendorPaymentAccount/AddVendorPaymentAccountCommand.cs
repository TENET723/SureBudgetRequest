using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.Abstractions.Services;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;

namespace SureBudgetRequest.Application.Vendors.Commands.AddVendorPaymentAccount;

public sealed record AddVendorPaymentAccountCommand(
    Guid VendorId,
    PaymentAccountType AccountType,
    string ProviderName,
    string AccountNumber,
    string AccountHolderName,
    string? BranchName = null,
    bool IsDefault = false,
    string? AttachmentFileName = null,
    string? AttachmentContentType = null,
    long? AttachmentSizeBytes = null,
    string? AttachmentPreUploadedPath = null) : IRequest<Result<Guid>>;

public sealed class AddVendorPaymentAccountCommandHandler
    : IRequestHandler<AddVendorPaymentAccountCommand, Result<Guid>>
{
    private readonly IVendorRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFileStorage _fileStorage;

    public AddVendorPaymentAccountCommandHandler(
        IVendorRepository repository,
        IUnitOfWork unitOfWork,
        IFileStorage fileStorage)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _fileStorage = fileStorage;
    }

    public async Task<Result<Guid>> Handle(AddVendorPaymentAccountCommand command, CancellationToken ct)
    {
        var vendor = await _repository.GetByIdAsync(command.VendorId, ct);
        if (vendor is null) return Result.Failure<Guid>(VendorErrors.GenericNotFound);

        string? storedAttachmentPath = null;
        if (!string.IsNullOrEmpty(command.AttachmentPreUploadedPath))
        {
            var ext = System.IO.Path.GetExtension(command.AttachmentFileName ?? "").ToLowerInvariant();
            if (ext is not ".pdf" and not ".png" and not ".jpg" and not ".jpeg" and not ".webp")
            {
                ext = ".bin";
            }
            storedAttachmentPath = $"vendors/{vendor.Id}/{Guid.NewGuid():N}{ext}";
            try
            {
                await _fileStorage.MoveAsync(command.AttachmentPreUploadedPath, storedAttachmentPath, ct);
            }
            catch (Exception ex)
            {
                return Result.Failure<Guid>($"Failed to store account proof file: {ex.Message}");
            }
        }

        VendorPaymentAccount account;
        try
        {
            account = vendor.AddPaymentAccount(
                command.AccountType,
                command.ProviderName,
                command.AccountNumber,
                command.AccountHolderName,
                command.BranchName,
                command.IsDefault,
                command.AttachmentFileName,
                storedAttachmentPath,
                command.AttachmentContentType,
                command.AttachmentSizeBytes);
        }
        catch (Exception ex)
        {
            if (storedAttachmentPath is not null)
            {
                await _fileStorage.DeleteAsync(storedAttachmentPath, ct);
            }
            return Result.Failure<Guid>(VendorErrors.ValidationError(ex.Message));
        }

        _repository.AddAccount(account);

        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            if (storedAttachmentPath is not null)
            {
                await _fileStorage.DeleteAsync(storedAttachmentPath, ct);
            }
            return Result.Failure<Guid>($"Database save failed: {ex.Message}");
        }

        return Result.Success(account.Id);
    }
}
