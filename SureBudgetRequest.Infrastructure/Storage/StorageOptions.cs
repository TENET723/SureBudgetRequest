namespace SureBudgetRequest.Infrastructure.Storage;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>
    /// Which storage backend to use.
    ///   "Supabase" (default) — uploads to a Supabase Storage bucket.
    ///   "Local"              — uploads to the local filesystem at <see cref="AttachmentsRoot"/>.
    /// Match case is case-insensitive.
    /// </summary>
    public string Provider { get; set; } = "Supabase";

    /// <summary>
    /// Root directory for attachment files when <see cref="Provider"/> is "Local".
    /// Default: App_Data/attachments (relative to the app's ContentRootPath).
    /// Ignored when Provider is "Supabase".
    /// </summary>
    public string AttachmentsRoot { get; set; } = "App_Data/attachments";

    /// <summary>
    /// How often the background cleanup service runs, in hours.
    /// Default: 6 hours.
    /// </summary>
    public int TempCleanIntervalHours { get; set; } = 6;

    /// <summary>
    /// How long a temporary file is preserved before being cleaned up, in hours.
    /// Default: 24 hours.
    /// </summary>
    public int TempExpirationHours { get; set; } = 24;
}
