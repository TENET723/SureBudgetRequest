using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.Abstractions.Services;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;

namespace SureBudgetRequest.Application.Vendors.Commands.CreateVendor;

public sealed record CreateVendorPaymentAccountDto(
    PaymentAccountType AccountType,
    string ProviderName,
    string AccountNumber,
    string AccountHolderName,
    string? BranchName = null,
    string? AttachmentFileName = null,
    string? AttachmentContentType = null,
    long? AttachmentSizeBytes = null,
    string? AttachmentPreUploadedPath = null);

public sealed record CreateVendorCommand(
    string Name,
    string? ContactPerson = null,
    string? Phone = null,
    string? Email = null,
    string? Address = null,
    IReadOnlyList<CreateVendorPaymentAccountDto>? InitialAccounts = null) : IRequest<Result<Guid>>;

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

        var storedAttachmentPaths = new List<string>();

        // If initial payment accounts were supplied, add each
        if (command.InitialAccounts is not null && command.InitialAccounts.Count > 0)
        {
            foreach (var acc in command.InitialAccounts)
            {
                if (string.IsNullOrWhiteSpace(acc.ProviderName) ||
                    string.IsNullOrWhiteSpace(acc.AccountNumber) ||
                    string.IsNullOrWhiteSpace(acc.AccountHolderName))
                {
                    continue;
                }

                string? storedAttachmentPath = null;
                if (!string.IsNullOrEmpty(acc.AttachmentPreUploadedPath))
                {
                    var ext = System.IO.Path.GetExtension(acc.AttachmentFileName ?? "").ToLowerInvariant();
                    if (ext is not ".pdf" and not ".png" and not ".jpg" and not ".jpeg" and not ".webp")
                    {
                        ext = ".bin";
                    }
                    storedAttachmentPath = $"vendors/{vendor.Id}/{Guid.NewGuid():N}{ext}";
                    try
                    {
                        await _fileStorage.MoveAsync(acc.AttachmentPreUploadedPath, storedAttachmentPath, ct);
                        storedAttachmentPaths.Add(storedAttachmentPath);
                    }
                    catch (Exception ex)
                    {
                        await CleanupStoredFilesAsync(storedAttachmentPaths);
                        return Result.Failure<Guid>($"Failed to store account proof file: {ex.Message}");
                    }
                }

                try
                {
                    vendor.AddPaymentAccount(
                        acc.AccountType,
                        acc.ProviderName,
                        acc.AccountNumber,
                        acc.AccountHolderName,
                        acc.BranchName,
                        attachmentFileName: acc.AttachmentFileName,
                        attachmentStoredPath: storedAttachmentPath,
                        attachmentContentType: acc.AttachmentContentType,
                        attachmentSizeBytes: acc.AttachmentSizeBytes);
                }
                catch (Exception ex)
                {
                    await CleanupStoredFilesAsync(storedAttachmentPaths);
                    return Result.Failure<Guid>(VendorErrors.ValidationError(ex.Message));
                }
            }
        }

        await _repository.AddAsync(vendor, ct);

        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch
        {
            await CleanupStoredFilesAsync(storedAttachmentPaths);
            throw;
        }

        return Result.Success(vendor.Id);
    }

    private async Task CleanupStoredFilesAsync(List<string> paths)
    {
        foreach (var path in paths)
        {
            try { await _fileStorage.DeleteAsync(path, CancellationToken.None); } catch { }
        }
    }
}
