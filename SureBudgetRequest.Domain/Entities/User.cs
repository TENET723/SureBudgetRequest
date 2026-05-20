using SureBudgetRequest.Domain.Enums;

namespace SureBudgetRequest.Domain.Entities;

public class User
{
    public Guid Id { get; private set; }
    public string Username { get; private set; } = null!;
    public string FullName { get; private set; } = null!;
    public string Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public DateTime? LastLoginAt { get; private set; }

    /// <summary>
    /// True when the user must change their password before doing anything else.
    /// Set on creation (initial password chosen by Admin) and on Admin-driven resets.
    /// Cleared after a successful self-service password change.
    /// </summary>
    public bool MustChangePassword { get; private set; }

    public string? SlackUserId { get; private set; }
    public Guid DepartmentId { get; private set; }
    public UserRole Role { get; private set; }

    /// <summary>
    /// Distinguishes the two Finance sub-types. Only meaningful when <see cref="Role"/> is <see cref="UserRole.Finance"/>.
    ///   - true  → "Finance Approver" (Type 1): can Approve, Reject, Send Back, AND Record Payment.
    ///   - false → "Finance Payer only" (Type 2): can Record Payment only.
    /// Automatically cleared when the role changes to anything other than Finance.
    /// </summary>
    public bool IsFinanceApprover { get; private set; }

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
        IsFinanceApprover = false; // default — Admin must explicitly grant approver rights
        CreatedAt = DateTime.UtcNow;
    }

    public void Deactivate() => IsActive = false;
    public void Reactivate() => IsActive = true;

    public void ChangeRole(UserRole newRole)
    {
        Role = newRole;

        // Approver flag is only meaningful for Finance users. Clear it automatically
        // whenever the role moves away from Finance so we never end up with a
        // non-Finance user carrying a stale "approver" bit.
        if (newRole != UserRole.Finance)
            IsFinanceApprover = false;
    }

    public void ChangeDepartment(Guid newDepartmentId) => DepartmentId = newDepartmentId;

    /// <summary>
    /// Sets the Finance-approver flag. Caller (Application layer) must ensure
    /// the user has <see cref="UserRole.Finance"/> before calling with <c>true</c>;
    /// the domain rejects mismatched combinations defensively.
    /// </summary>
    public void SetFinanceApprover(bool value)
    {
        if (value && Role != UserRole.Finance)
            throw new InvalidOperationException(
                "Only a user with the Finance role can be marked as a Finance Approver.");

        IsFinanceApprover = value;
    }

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

    public void RecordLogin() => LastLoginAt = DateTime.UtcNow;
}
