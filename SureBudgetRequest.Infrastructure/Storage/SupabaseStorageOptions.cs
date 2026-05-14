namespace SureBudgetRequest.Infrastructure.Storage;

/// <summary>
/// Configuration for the Supabase Storage backend.
/// Bound from the "Supabase" config section.
/// </summary>
public sealed class SupabaseStorageOptions
{
    public const string SectionName = "Supabase";

    /// <summary>
    /// Project URL, e.g. <c>https://abcd1234.supabase.co</c>.
    /// (NOT the database pooler URL — that's separate.)
    /// </summary>
    public string Url { get; set; } = "";

    /// <summary>
    /// The service-role key. Bypasses Storage RLS, consistent with how the rest
    /// of the app talks to Supabase. Treat as a secret — never expose to clients.
    /// </summary>
    public string ServiceRoleKey { get; set; } = "";

    /// <summary>Storage bucket name. Must be created in Supabase ahead of time.</summary>
    public string AttachmentsBucket { get; set; } = "budget-attachments";
}
