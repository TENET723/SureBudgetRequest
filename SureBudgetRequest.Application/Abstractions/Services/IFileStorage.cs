namespace SureBudgetRequest.Application.Abstractions.Services;

/// <summary>
/// Abstracts file storage so the Web and Application layers can save/read/delete
/// attachments without knowing whether storage is local disk or a cloud provider
/// (Supabase Storage by default; local disk as a dev fallback).
///
/// Lives in Application (not Infrastructure) so command handlers can orchestrate
/// file save + DB write inside a single transaction-shaped unit of work.
///
/// <para>
/// <b>Stored path contract:</b> implementations return a provider-relative path
/// (e.g. <c>{requestId}/{guid}_{fileName}</c>), NOT an absolute disk path. The
/// implementation is responsible for resolving that relative path to wherever
/// the file actually lives. This keeps stored paths portable across providers.
/// </para>
/// </summary>
public interface IFileStorage
{
    /// <summary>
    /// Saves a file stream and returns the relative stored path suitable for
    /// passing to <c>BudgetRequest.AddAttachment</c>.
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
