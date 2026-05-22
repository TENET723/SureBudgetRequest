namespace SureBudgetRequest.Domain.Entities;

/// <summary>
/// Chart of Account master record. Each approved budget request is tagged with
/// one Coa so Finance can book it against the right account.
///
/// The natural identifier is <see cref="Code"/> (e.g. "5110") but the surrogate
/// <see cref="Id"/> is used as the FK on <c>BudgetRequest</c> — that way an admin
/// can rename a code without rewriting historical request rows.
/// </summary>
public class Coa
{
    public Guid Id { get; private set; }
    public string Code { get; private set; } = null!;        // unique, e.g. "5110"
    public string Name { get; private set; } = null!;        // e.g. "Office Supplies"
    public string? Description { get; private set; }         // optional longer description
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // For EF Core
    private Coa() { }

    public Coa(string code, string name, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Code is required.", nameof(code));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));

        //Id = Guid.NewGuid();
        Code = code.Trim();
        Name = name.Trim();
        Description = NormalizeOptional(description);
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
    }

    public void Rename(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("Name is required.", nameof(newName));
        Name = newName.Trim();
    }

    public void ChangeCode(string newCode)
    {
        if (string.IsNullOrWhiteSpace(newCode))
            throw new ArgumentException("Code is required.", nameof(newCode));
        Code = newCode.Trim();
    }

    public void ChangeDescription(string? newDescription)
        => Description = NormalizeOptional(newDescription);

    public void Deactivate() => IsActive = false;
    public void Reactivate() => IsActive = true;

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
