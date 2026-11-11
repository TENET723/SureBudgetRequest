using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.Abstractions.Services;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;

namespace SureBudgetRequest.Application.Vendors.Commands.CreateVendor;

public sealed record CreateVendorCommand(
    string Name,
    string? ContactPerson = null,
    string? Phone = null,
    string? Email = null,
    string? Address = null,
    PaymentAccountType? InitialAccountType = null,
    string? InitialProviderName = null,
    string? InitialAccountNumber = null,
    string? InitialAccountHolderName = null,
    string? InitialBranchName = null,
    string? AttachmentFileName = null,
    string? AttachmentContentType = null,
    long? AttachmentSizeBytes = null,
    string? AttachmentPreUploadedPath = null) : IRequest<Result<Guid>>;

public sealed class CreateVendorCommandHandler
    : IRequestHandler<CreateVendorCommand, Result<Guid>>
{
    private readonly IVendorRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFileStorage _fileStorage;

    public CreateVendorCommandHandler(
        IVendorRepository repository,
        IUnitOfWork unitOfWork,
        IFileStorage fileStorage)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _fileStorage = fileStorage;
    }

    public async Task<Result<Guid>> Handle(CreateVendorCommand command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
            return Result.Failure<Guid>(VendorErrors.ValidationError("Vendor name is required."));

        var existing = await _repository.GetByNameAsync(command.Name, ct);
        if (existing is not null)
            return Result.Failure<Guid>(VendorErrors.AlreadyExists(command.Name.Trim()));

        Vendor vendor;
        try
        {
            vendor = new Vendor(
                command.Name,
                command.ContactPerson,
                command.Phone,
                command.Email,
                command.Address);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<Guid>(VendorErrors.ValidationError(ex.Message));
        }

        string? storedAttachmentPath = null;

        // If an initial payment account was supplied, add it
        if (command.InitialAccountType.HasValue &&
            !string.IsNullOrWhiteSpace(command.InitialProviderName) &&
            !string.IsNullOrWhiteSpace(command.InitialAccountNumber) &&
            !string.IsNullOrWhiteSpace(command.InitialAccountHolderName))
        {
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

            try
            {
                vendor.AddPaymentAccount(
                    command.InitialAccountType.Value,
                    command.InitialProviderName,
                    command.InitialAccountNumber,
                    command.InitialAccountHolderName,
                    command.InitialBranchName,
                    attachmentFileName: command.AttachmentFileName,
                    attachmentStoredPath: storedAttachmentPath,
                    attachmentContentType: command.AttachmentContentType,
                    attachmentSizeBytes: command.AttachmentSizeBytes);
            }
            catch (Exception ex)
            {
                if (storedAttachmentPath is not null)
                {
                    await _fileStorage.DeleteAsync(storedAttachmentPath, ct);
                }
                return Result.Failure<Guid>(VendorErrors.ValidationError(ex.Message));
            }
        }

        await _repository.AddAsync(vendor, ct);

        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch
        {
            if (storedAttachmentPath is not null)
            {
                await _fileStorage.DeleteAsync(storedAttachmentPath, ct);
            }
            throw;
        }

        return Result.Success(vendor.Id);
    }
}
