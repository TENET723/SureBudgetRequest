using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.Abstractions.Services;
using SureBudgetRequest.Application.BudgetRequests.Common;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Enums;

namespace SureBudgetRequest.Application.BudgetRequests.Commands.UploadAttachment;

/// <summary>
/// Uploads a new attachment to a budget request.
///
/// Flow:
///   1. Validate constraints (size, MIME).
///   2. Load the aggregate; the aggregate enforces status + file-count invariants.
///   3. Save the file via <see cref="IFileStorage"/> to get a stored path.
///   4. Call <c>BudgetRequest.AddAttachment(...)</c> — if it fails, delete the file
///      we just saved so we don't leave an orphan.
///   5. <c>SaveChangesAsync</c>. If the DB write throws, also delete the file.
///
/// Returns the new Attachment Id on success.
/// </summary>
public sealed record UploadAttachmentCommand(
    Guid BudgetRequestId,
    Guid UploadedByUserId,
    string FileName,
    string ContentType,
    long SizeBytes,
    Stream Content,
    AttachmentCategory Category = AttachmentCategory.General,
    string? PreUploadedPath = null) : IRequest<Result<Guid>>;

public sealed class UploadAttachmentCommandHandler
    : IRequestHandler<UploadAttachmentCommand, Result<Guid>>
{
    private readonly IBudgetRequestRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFileStorage _fileStorage;

    public UploadAttachmentCommandHandler(
        IBudgetRequestRepository repository,
        IUnitOfWork unitOfWork,
        IFileStorage fileStorage)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _fileStorage = fileStorage;
    }

    public async Task<Result<Guid>> Handle(
        UploadAttachmentCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Pre-flight validation (cheap checks before we touch storage).
        if (string.IsNullOrWhiteSpace(command.FileName))
            return Result<Guid>.Failure("File name is required.");
        if (command.SizeBytes <= 0)
            return Result<Guid>.Failure("File size must be greater than zero.");
        if (command.SizeBytes > AttachmentConstraints.MaxBytes)
            return Result<Guid>.Failure(
                $"File exceeds the {AttachmentConstraints.MaxBytes / (1024 * 1024)} MB limit.");
        if (!AttachmentConstraints.IsAllowedContentType(command.ContentType))
            return Result<Guid>.Failure($"File type '{command.ContentType}' is not allowed.");

        // 2. Load the aggregate.
        var budgetRequest = await _repository.GetByIdAsync(command.BudgetRequestId, cancellationToken);
        if (budgetRequest is null)
            return Result<Guid>.Failure("Budget request not found.");

        // 3. Save or Move the file in storage. If the aggregate rejects it in step 4,
        //    we'll clean it up.
        string storedPath;
        try
        {
            if (!string.IsNullOrEmpty(command.PreUploadedPath))
            {
                var ext = System.IO.Path.GetExtension(command.FileName).ToLowerInvariant();
                if (ext is not ".pdf" and not ".png" and not ".jpg" and not ".jpeg" and not ".gif" and not ".webp"
                    and not ".doc" and not ".docx" and not ".xls" and not ".xlsx" and not ".txt" and not ".csv")
                {
                    ext = ".bin";
                }
                storedPath = $"{command.BudgetRequestId}/{Guid.NewGuid():N}{ext}";
                await _fileStorage.MoveAsync(command.PreUploadedPath, storedPath, cancellationToken);
            }
            else
            {
                storedPath = await _fileStorage.SaveAsync(
                    command.BudgetRequestId.ToString(),
                    command.FileName,
                    command.Content,
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            return Result<Guid>.Failure($"Failed to store the file: {ex.Message}");
        }

        // 4. Domain-level add (enforces status, requester, count).
        var addResult = budgetRequest.AddAttachment(
            command.FileName,
            storedPath,
            command.ContentType,
            command.SizeBytes,
            command.UploadedByUserId,
            command.Category);

        if (addResult.IsFailure)
        {
            await TryDeleteFromStorageAsync(storedPath);
            return Result<Guid>.Failure(addResult.Error!);
        }

        // 5. Persist. If the DB write fails, roll back the stored file too.
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await TryDeleteFromStorageAsync(storedPath);
            throw;
        }

        // EF Core populates the Id during SaveChanges; the last attachment is the one we just added.
        var newAttachmentId = budgetRequest.Attachments[^1].Id;
        return Result<Guid>.Success(newAttachmentId);
    }

    private async Task TryDeleteFromStorageAsync(string storedPath)
    {
        try
        {
            await _fileStorage.DeleteAsync(storedPath, CancellationToken.None);
        }
        catch
        {
            // Best-effort cleanup. A leftover file in storage is preferable to throwing
            // out of an already-failed handler and masking the original error.
        }
    }
}
