using SureBudgetRequest.Domain.Enums;

namespace SureBudgetRequest.Domain.Entities;

public class User
{
    public Guid Id { get; private set; }
    public string Username { get; private set; } = null!;
    public string FullName { get; private set; } = null!;
    public string? Email { get; private set; }
    public string? SlackUserId { get; private set; }
    public Guid DepartmentId { get; private set; }
    public UserRole Role { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // For EF Core
    private User() { }

    public User(string username, string fullName, Guid departmentId, UserRole role)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username is required.", nameof(username));
        if (string.IsNullOrWhiteSpace(fullName))
            throw new ArgumentException("Full name is required.", nameof(fullName));

        Id = Guid.NewGuid();
        Username = username;
        FullName = fullName;
        DepartmentId = departmentId;
        Role = role;
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
    }

    public void Deactivate() => IsActive = false;
    public void Reactivate() => IsActive = true;
    public void ChangeRole(UserRole newRole) => Role = newRole;
    public void ChangeDepartment(Guid newDepartmentId) => DepartmentId = newDepartmentId;
    public void SetEmail(string? email) => Email = email;
    public void SetSlackUserId(string? slackId) => SlackUserId = slackId;
    public void Rename(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new ArgumentException("Full name is required.", nameof(fullName));
        FullName = fullName;
    }
}
