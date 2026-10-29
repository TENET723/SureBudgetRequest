using System.Text.Json;

namespace SureBudgetRequest.Application.Tags.Models;

/// <summary>
/// Configuration for tag visibility on ticket detail pages.
/// Stored as JSON in <c>AppSetting</c> under the key <see cref="SettingKey"/>.
/// </summary>
public sealed class TagVisibilityConfig
{
    public const string SettingKey = "TagSettings:Config";

    public Guid? FinanceDepartmentId { get; set; }
    public Guid? ManagementDepartmentId { get; set; }

    /// <summary>
    /// True only when both Finance and Management department designations have been explicitly configured.
    /// When false, the cross-department visibility matrix is considered unconfigured and inactive.
    /// </summary>
    public bool IsConfigured => FinanceDepartmentId.HasValue && ManagementDepartmentId.HasValue;

    public Dictionary<string, List<string>> RoleVisibilityMatrix { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Requester"] = new() { "Management", "Finance" },
        ["Management"] = new() { "Requester" },
        ["Finance"] = new()
    };

    public static TagVisibilityConfig CreateDefault() => new();

    public string ToJson() => JsonSerializer.Serialize(this);

    public static TagVisibilityConfig FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return CreateDefault();

        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            var config = JsonSerializer.Deserialize<TagVisibilityConfig>(json, options);
            if (config is null)
                return CreateDefault();

            var matrix = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            if (config.RoleVisibilityMatrix is not null)
            {
                foreach (var kvp in config.RoleVisibilityMatrix)
                {
                    matrix[kvp.Key] = kvp.Value ?? new();
                }
            }

            if (!matrix.ContainsKey("Requester"))
                matrix["Requester"] = new() { "Management", "Finance" };
            if (!matrix.ContainsKey("Management"))
                matrix["Management"] = new() { "Requester" };
            if (!matrix.ContainsKey("Finance"))
                matrix["Finance"] = new();

            config.RoleVisibilityMatrix = matrix;
            return config;
        }
        catch
        {
            return CreateDefault();
        }
    }
}
