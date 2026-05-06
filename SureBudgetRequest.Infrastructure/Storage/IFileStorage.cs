namespace SureBudgetRequest.Infrastructure.Storage;

/// <summary>
/// Abstracts file storage so Web can upload attachments without knowing
/// whether storage is local disk or cloud (Supabase Storage in v2).
/// </summary>
public interface IFileStorage
{
    /// <summary>
    /// Saves a file stream and returns the stored path suitable for
    /// passing to <see cref="Domain.Entities.BudgetRequest.AddAttachment"/>.
    /// </summary>
    Task<string> SaveAsync(
        Guid budgetRequestId,
        string originalFileName,
        Stream content,
        CancellationToken cancellationToken = default);

    /// <summary>Returns a stream to read a previously stored file.</summary>
    Task<Stream> ReadAsync(string storedPath, CancellationToken cancellationToken = default);

    /// <summary>Deletes a stored file. No-op if the path does not exist.</summary>
    Task DeleteAsync(string storedPath, CancellationToken cancellationToken = default);
}
