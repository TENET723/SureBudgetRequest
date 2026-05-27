namespace SureBudgetRequest.Domain.Entities;

public class AppSetting
{
    public string Key { get; private set; } = null!;
    public string Value { get; private set; } = null!;
    public string? Description { get; private set; }

    // For EF Core
    private AppSetting() { }

    public AppSetting(string key, string value, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key is required.", nameof(key));
        
        Key = key;
        Value = value;
        Description = description;
    }

    public void UpdateValue(string newValue) => Value = newValue;
    public void UpdateDescription(string? newDescription) => Description = newDescription;
}
