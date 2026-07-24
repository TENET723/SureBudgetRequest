namespace SureBudgetRequest.Application.BudgetRequests.Common;

/// <summary>
/// Hard limits applied to attachment uploads. Enforced in the
/// <c>UploadAttachmentCommand</c> handler before the file is saved to storage.
/// </summary>
public static class AttachmentConstraints
{
    /// <summary>Maximum size per file: 30 MB.</summary>
    public const long MaxBytes = 30L * 1024 * 1024;

    /// <summary>Maximum number of attachments allowed on a single request.</summary>
    /// <remarks>Also enforced as an invariant inside <c>BudgetRequest.AddAttachment</c>.</remarks>
    public const int MaxFilesPerRequest = 10;

    /// <summary>
    /// MIME types we accept. Whitelist — anything else is rejected at upload time.
    /// Covers PDFs, common image formats, Office docs, and plain text/CSV.
    /// </summary>
    public static readonly IReadOnlySet<string> AllowedContentTypes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // PDFs
            "application/pdf",
            // Images
            "image/png",
            "image/jpeg",
            "image/gif",
            "image/webp",
            // Word
            "application/msword",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            // Excel
            "application/vnd.ms-excel",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            // Plain text / CSV
            "text/plain",
            "text/csv",
        };

    public static bool IsAllowedContentType(string? contentType) =>
        !string.IsNullOrWhiteSpace(contentType) && AllowedContentTypes.Contains(contentType);
}
