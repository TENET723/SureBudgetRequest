using Microsoft.Extensions.Options;
using SureBudgetRequest.Application.Abstractions.Services;

namespace SureBudgetRequest.Infrastructure.Storage;

/// <summary>
/// Stores attachments on the local filesystem under
/// <c>{AttachmentsRoot}/{requestId}/{guid}_{originalFileName}</c>.
///
/// <para>
/// Stored paths are RELATIVE — e.g. <c>"{requestId}/{guid}_{fileName}"</c> — to
/// match the contract documented on <see cref="IFileStorage"/>. The root prefix
/// is prepended internally when reading/deleting. This keeps stored paths
/// portable across providers (Supabase, local, future S3, etc.).
/// </para>
///
/// Use this provider by setting <c>Storage:Provider</c> to <c>"Local"</c>.
/// Default is Supabase.
/// </summary>
public sealed class LocalFileStorage : IFileStorage
{
    private readonly string _rootPath;

    public LocalFileStorage(IOptions<StorageOptions> options)
    {
        _rootPath = options.Value.AttachmentsRoot;
    }

    public async Task<string> SaveAsync(
        Guid budgetRequestId,
        string originalFileName,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        var safeFileName = SanitizeFileName(originalFileName);
        var uniqueName = $"{Guid.NewGuid():N}_{safeFileName}";

        // Relative path is what we return + store in DB.
        var relativePath = $"{budgetRequestId}/{uniqueName}";

        var absoluteDir = Path.Combine(_rootPath, budgetRequestId.ToString());
        Directory.CreateDirectory(absoluteDir);

        var absolutePath = Path.Combine(_rootPath, relativePath);

        await using var fileStream = new FileStream(
            absolutePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);

        await content.CopyToAsync(fileStream, cancellationToken);
        return relativePath;
    }

    public Task<Stream> ReadAsync(string storedPath, CancellationToken cancellationToken = default)
    {
        var absolutePath = Path.Combine(_rootPath, storedPath);

        if (!File.Exists(absolutePath))
            throw new FileNotFoundException("Attachment file not found.", absolutePath);

        Stream stream = new FileStream(
            absolutePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);

        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storedPath, CancellationToken cancellationToken = default)
    {
        var absolutePath = Path.Combine(_rootPath, storedPath);
        if (File.Exists(absolutePath))
            File.Delete(absolutePath);

        return Task.CompletedTask;
    }

    private static string SanitizeFileName(string fileName)
    {
        // Strip directory traversal, keep only the file name portion
        var name = Path.GetFileName(fileName);
        // Replace any remaining invalid chars with underscores
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c));
    }
}
