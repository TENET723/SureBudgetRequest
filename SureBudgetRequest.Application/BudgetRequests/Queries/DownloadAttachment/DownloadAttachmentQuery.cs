using MediatR;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.Abstractions.Services;
using SureBudgetRequest.Domain.Common;

namespace SureBudgetRequest.Application.BudgetRequests.Queries.DownloadAttachment;

/// <summary>
/// Resolves an attachment to its physical content, so a Web endpoint can stream it
/// to the browser. Any authenticated user can download (per the agreed
/// authorization model — internal network only, v1).
/// </summary>
public sealed record DownloadAttachmentQuery(Guid AttachmentId)
    : IRequest<Result<AttachmentContent>>;

/// <summary>
/// Returned to the Web endpoint, which is responsible for streaming
/// <see cref="Stream"/> to the response and disposing it.
/// </summary>
public sealed record AttachmentContent(
    Stream Stream,
    string FileName,
    string ContentType,
    long SizeBytes);

public sealed class DownloadAttachmentQueryHandler
    : IRequestHandler<DownloadAttachmentQuery, Result<AttachmentContent>>
{
    private readonly IBudgetRequestRepository _repository;
    private readonly IFileStorage _fileStorage;

    public DownloadAttachmentQueryHandler(
        IBudgetRequestRepository repository,
        IFileStorage fileStorage)
    {
        _repository = repository;
        _fileStorage = fileStorage;
    }

    public async Task<Result<AttachmentContent>> Handle(
        DownloadAttachmentQuery query,
        CancellationToken cancellationToken)
    {
        var attachment = await _repository.GetAttachmentByIdAsync(query.AttachmentId, cancellationToken);
        if (attachment is null)
            return Result<AttachmentContent>.Failure("Attachment not found.");

        Stream stream;
        try
        {
            stream = await _fileStorage.ReadAsync(attachment.StoredPath, cancellationToken);
        }
        catch (FileNotFoundException)
        {
            return Result<AttachmentContent>.Failure("Attachment file is missing from storage.");
        }
        catch (Exception ex)
        {
            return Result<AttachmentContent>.Failure($"Failed to read attachment from storage: {ex.Message}");
        }

        return Result<AttachmentContent>.Success(new AttachmentContent(
            stream,
            attachment.FileName,
            attachment.ContentType,
            attachment.SizeBytes));
    }
}
