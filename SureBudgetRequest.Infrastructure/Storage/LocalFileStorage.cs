using Microsoft.Extensions.Options;

namespace SureBudgetRequest.Infrastructure.Storage;

/// <summary>
/// Stores attachments on the local filesystem under
/// <c>{AttachmentsRoot}/{requestId}/{guid}_{originalFileName}</c>.
///
/// v2 replacement: swap this for a SupabaseFileStorage implementation
/// without touching any other code.
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

        var directory = Path.Combine(_rootPath, budgetRequestId.ToString());
        Directory.CreateDirectory(directory);

        var storedPath = Path.Combine(directory, uniqueName);

        await using var fileStream = new FileStream(
            storedPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);

        await content.CopyToAsync(fileStream, cancellationToken);
        return storedPath;
    }

    public Task<Stream> ReadAsync(string storedPath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(storedPath))
            throw new FileNotFoundException("Attachment file not found.", storedPath);

        Stream stream = new FileStream(
            storedPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);

        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storedPath, CancellationToken cancellationToken = default)
    {
        if (File.Exists(storedPath))
            File.Delete(storedPath);

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
