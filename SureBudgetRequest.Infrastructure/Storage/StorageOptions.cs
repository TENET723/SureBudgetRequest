namespace SureBudgetRequest.Infrastructure.Storage;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>
    /// Root directory for attachment files.
    /// Default: App_Data/attachments (relative to the app's ContentRootPath).
    /// </summary>
    public string AttachmentsRoot { get; set; } = "App_Data/attachments";
}
