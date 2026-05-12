using SureBudgetRequest.Domain.Enums;

namespace SureBudgetRequest.Domain.Entities;

public class User
{
    public Guid Id { get; private set; }
    public string Username { get; private set; } = null!;
    public string FullName { get; private set; } = null!;
    public string Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;

    /// <summary>
    /// True when the user must change their password before doing anything else.
    /// Set on creation (initial password chosen by Admin) and on Admin-driven resets.
    /// Cleared after a successful self-service password change.
    /// </summary>
    public bool MustChangePassword { get; private set; }

    public string? SlackUserId { get; private set; }
    public Guid DepartmentId { get; private set; }
    public UserRole Role { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // For EF Core
    private User() { }

    public User(string username, string fullName, string email, Guid departmentId, UserRole role)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username is required.", nameof(username));
        if (string.IsNullOrWhiteSpace(fullName))
            throw new ArgumentException("Full name is required.", nameof(fullName));
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required.", nameof(email));

        Username = username;
        FullName = fullName;
        Email = NormalizeEmail(email);
        DepartmentId = departmentId;
        Role = role;
        IsActive = true;
        MustChangePassword = true; // default — Admin sets initial password, user must change on first login
        CreatedAt = DateTime.UtcNow;
    }

    public void Deactivate() => IsActive = false;
    public void Reactivate() => IsActive = true;
    public void ChangeRole(UserRole newRole) => Role = newRole;
    public void ChangeDepartment(Guid newDepartmentId) => DepartmentId = newDepartmentId;

    public void SetEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required.", nameof(email));
        Email = NormalizeEmail(email);
    }

    public void SetSlackUserId(string? slackId) => SlackUserId = slackId;

    public void Rename(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new ArgumentException("Full name is required.", nameof(fullName));
        FullName = fullName;
    }

    /// <summary>
    /// Sets the password hash. The caller (a command handler) is responsible for
    /// computing the hash via <c>IPasswordHasher</c>. Domain never sees plaintext.
    /// </summary>
    /// <param name="passwordHash">An already-hashed password value.</param>
    /// <param name="mustChangeOnNextLogin">
    /// True for Admin-set initial passwords and Admin resets; false for self-service changes.
    /// </param>
    public void SetPasswordHash(string passwordHash, bool mustChangeOnNextLogin)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("Password hash is required.", nameof(passwordHash));
        PasswordHash = passwordHash;
        MustChangePassword = mustChangeOnNextLogin;
    }

    /// <summary>Email is stored lowercase + trimmed so uniqueness is case-insensitive.</summary>
    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
