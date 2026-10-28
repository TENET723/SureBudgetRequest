using System.Text.RegularExpressions;

namespace SureBudgetRequest.Domain.Entities;

/// <summary>
/// Tag master record, isolated at the department level.
/// Reusable label that users under the same department can attach to tickets.
/// </summary>
public class Tag
{
    private static readonly Regex HexColorRegex = new(@"^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{3})$", RegexOptions.Compiled);

    public Guid Id { get; private set; }
    public Guid DepartmentId { get; private set; }
    public string Name { get; private set; } = null!;
    public string? ColorHex { get; private set; }
    public string? FontColorHex { get; private set; }
    public bool IsActive { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // For EF Core
    private Tag() { }

    public Tag(Guid departmentId, string name, Guid createdByUserId, string? colorHex = null, string? fontColorHex = null)
    {
        if (departmentId == Guid.Empty)
            throw new ArgumentException("Department ID is required.", nameof(departmentId));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Tag name is required.", nameof(name));
        if (createdByUserId == Guid.Empty)
            throw new ArgumentException("Created by user ID is required.", nameof(createdByUserId));

        //Id = Guid.NewGuid();
        DepartmentId = departmentId;
        Name = name.Trim();
        ColorHex = NormalizeColorHex(colorHex);
        FontColorHex = NormalizeColorHex(fontColorHex);
        CreatedByUserId = createdByUserId;
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
    }

    public void Rename(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("Tag name is required.", nameof(newName));

        Name = newName.Trim();
    }

    public void SetColor(string? colorHex)
    {
        ColorHex = NormalizeColorHex(colorHex);
    }

    public void SetFontColor(string? fontColorHex)
    {
        FontColorHex = NormalizeColorHex(fontColorHex);
    }

    public void SetColors(string? colorHex, string? fontColorHex)
    {
        ColorHex = NormalizeColorHex(colorHex);
        FontColorHex = NormalizeColorHex(fontColorHex);
    }

    public void Deactivate() => IsActive = false;
    public void Reactivate() => IsActive = true;

    private static string? NormalizeColorHex(string? colorHex)
    {
        if (string.IsNullOrWhiteSpace(colorHex))
            return null;

        var trimmed = colorHex.Trim();
        if (!HexColorRegex.IsMatch(trimmed))
            throw new ArgumentException("Color must be a valid hex color code (e.g., #3B82F6 or #FFF).", nameof(colorHex));

        return trimmed.ToUpperInvariant();
    }
}
