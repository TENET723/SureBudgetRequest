using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using SureBudgetRequest.Application.Abstractions.Services;

namespace SureBudgetRequest.Infrastructure.Storage;

/// <summary>
/// Stores attachments in a Supabase Storage bucket. Uses the REST API directly
/// (no SDK dependency) so we can stream both upload and download.
///
/// Authenticated with the service-role key — bypasses RLS, consistent with how
/// the rest of the app talks to Supabase. The bucket itself should be set to
/// PRIVATE in the Supabase dashboard.
///
/// Stored paths returned from <see cref="SaveAsync"/> are bucket-relative, e.g.
/// <c>"{requestId}/{guid}_{sanitized}"</c>. The bucket name lives in config and
/// is not embedded in the stored path — that makes bucket changes painless.
/// </summary>
public sealed class SupabaseFileStorage : IFileStorage
{
    private readonly HttpClient _http;
    private readonly SupabaseStorageOptions _options;

    public SupabaseFileStorage(HttpClient http, IOptions<SupabaseStorageOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public async Task<string> SaveAsync(
        Guid budgetRequestId,
        string originalFileName,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        var safeName = SanitizeFileName(originalFileName);
        var relativePath = $"{budgetRequestId}/{Guid.NewGuid():N}_{safeName}";

        // POST /storage/v1/object/{bucket}/{path}
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/storage/v1/object/{Uri.EscapeDataString(_options.AttachmentsBucket)}/{EncodePath(relativePath)}");

        var streamContent = new StreamContent(content);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(GuessMime(originalFileName));
        request.Content = streamContent;

        // x-upsert=false: fail if a key collides (we use a GUID, so this should never happen).
        request.Headers.Add("x-upsert", "false");

        using var response = await _http.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Supabase Storage upload failed: {(int)response.StatusCode} {response.ReasonPhrase}. Body: {body}");
        }

        return relativePath;
    }

    public async Task<Stream> ReadAsync(string storedPath, CancellationToken cancellationToken = default)
    {
        // GET /storage/v1/object/{bucket}/{path}
        // With the service-role bearer header, this works for private buckets.
        var response = await _http.GetAsync(
            $"/storage/v1/object/{Uri.EscapeDataString(_options.AttachmentsBucket)}/{EncodePath(storedPath)}",
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new FileNotFoundException("Attachment file not found in Supabase Storage.", storedPath);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            response.Dispose();
            throw new InvalidOperationException(
                $"Supabase Storage download failed: {(int)response.StatusCode} {response.ReasonPhrase}. Body: {body}");
        }

        // Caller is responsible for disposing the returned stream.
        return await response.Content.ReadAsStreamAsync(cancellationToken);
    }

    public async Task DeleteAsync(string storedPath, CancellationToken cancellationToken = default)
    {
        // DELETE /storage/v1/object/{bucket}/{path}
        using var response = await _http.DeleteAsync(
            $"/storage/v1/object/{Uri.EscapeDataString(_options.AttachmentsBucket)}/{EncodePath(storedPath)}",
            cancellationToken);

        // 404 is a no-op (file already gone). Any other error bubbles.
        if (response.StatusCode == HttpStatusCode.NotFound) return;

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Supabase Storage delete failed: {(int)response.StatusCode} {response.ReasonPhrase}. Body: {body}");
        }
    }

    // ── helpers ────────────────────────────────────────────────────────────────

    /// <summary>
    /// URL-encodes each path segment but preserves '/' separators.
    /// </summary>
    private static string EncodePath(string relativePath)
    {
        var parts = relativePath.Split('/');
        return string.Join('/', parts.Select(Uri.EscapeDataString));
    }

    /// <summary>
    /// Strip directory traversal and replace any invalid filename chars with underscores.
    /// </summary>
    private static string SanitizeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName);
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c));
    }

    /// <summary>
    /// Coarse MIME guess from extension. The Application layer has already validated
    /// the client-supplied Content-Type against the whitelist, so this is just for
    /// what we tell Supabase to store on the object metadata.
    /// </summary>
    private static string GuessMime(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".pdf"  => "application/pdf",
            ".png"  => "image/png",
            ".jpg"  => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".gif"  => "image/gif",
            ".webp" => "image/webp",
            ".doc"  => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls"  => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".txt"  => "text/plain",
            ".csv"  => "text/csv",
            _       => "application/octet-stream"
        };
    }
}
