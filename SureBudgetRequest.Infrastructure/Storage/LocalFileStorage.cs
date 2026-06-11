using Microsoft.Extensions.Options;
using SureBudgetRequest.Application.Abstractions.Services;

namespace SureBudgetRequest.Infrastructure.Storage;

/// <summary>
/// Stores attachments on the local filesystem under
/// <c>{AttachmentsRoot}/{requestId}/{guid}{ext}</c>.
///
/// <para>
/// Stored paths are RELATIVE — e.g. <c>"{requestId}/{guid}{ext}"</c> — to
/// match the contract documented on <see cref="IFileStorage"/>. The root prefix
/// is prepended internally when reading/deleting. The original filename never
/// enters the path (it lives on <c>Attachment.FileName</c>), keeping the key
/// format identical to <see cref="SupabaseFileStorage"/>. This keeps stored
/// paths portable across providers (Supabase, local, future S3, etc.).
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
        var uniqueName = $"{Guid.NewGuid():N}{SafeExtension(originalFileName)}";

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

    /// <summary>
    /// Returns a storage-safe extension derived from the original filename.
    /// Only known extensions pass through (lowercased); anything else becomes
    /// ".bin". Mirrors <see cref="SupabaseFileStorage"/> so key formats stay
    /// identical across providers.
    /// </summary>
    private static string SafeExtension(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext is ".pdf" or ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp"
            or ".doc" or ".docx" or ".xls" or ".xlsx" or ".txt" or ".csv"
            ? ext
            : ".bin";
    }
}
