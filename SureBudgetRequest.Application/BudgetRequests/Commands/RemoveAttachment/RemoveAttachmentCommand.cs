using MediatR;
using Microsoft.Extensions.Logging;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.Abstractions.Services;
using SureBudgetRequest.Domain.Common;

namespace SureBudgetRequest.Application.BudgetRequests.Commands.RemoveAttachment;

/// <summary>
/// Removes an attachment from a budget request.
///
/// Order matters: the DB row is removed first (inside a transaction), then we
/// try to delete the physical file. If the file delete fails we log it but
/// don't roll back the DB — the row is the source of truth, and a leftover
/// orphan blob is recoverable later.
///
/// Authorization (requester-only, Draft/SentBack-only) is enforced inside
/// <c>BudgetRequest.RemoveAttachment</c>.
/// </summary>
public sealed record RemoveAttachmentCommand(
    Guid BudgetRequestId,
    Guid AttachmentId,
    Guid ByUserId) : IRequest<Result>;

public sealed class RemoveAttachmentCommandHandler
    : IRequestHandler<RemoveAttachmentCommand, Result>
{
    private readonly IBudgetRequestRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFileStorage _fileStorage;
    private readonly ILogger<RemoveAttachmentCommandHandler> _logger;

    public RemoveAttachmentCommandHandler(
        IBudgetRequestRepository repository,
        IUnitOfWork unitOfWork,
        IFileStorage fileStorage,
        ILogger<RemoveAttachmentCommandHandler> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _fileStorage = fileStorage;
        _logger = logger;
    }

    public async Task<Result> Handle(
        RemoveAttachmentCommand command,
        CancellationToken cancellationToken)
    {
        var budgetRequest = await _repository.GetByIdAsync(command.BudgetRequestId, cancellationToken);
        if (budgetRequest is null)
            return Result.Failure("Budget request not found.");

        var removeResult = budgetRequest.RemoveAttachment(command.AttachmentId, command.ByUserId);
        if (removeResult.IsFailure)
            return Result.Failure(removeResult.Error!);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // DB row is gone — now delete the physical file. Best-effort.
        var storedPath = removeResult.Value!;
        try
        {
            await _fileStorage.DeleteAsync(storedPath, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Removed attachment {AttachmentId} from request {RequestId}, but failed to delete the stored file at {StoredPath}. Manual cleanup may be required.",
                command.AttachmentId, command.BudgetRequestId, storedPath);
        }

        return Result.Success();
    }
}
