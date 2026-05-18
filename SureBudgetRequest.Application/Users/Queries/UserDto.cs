using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;

namespace SureBudgetRequest.Application.Users.Queries;

public sealed record UserDto(
    Guid Id,
    string Username,
    string FullName,
    string? Email,
    string? SlackUserId,
    Guid DepartmentId,
    UserRole Role,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? LastLoginAt)
{
    public static UserDto FromEntity(User u) => new(
        u.Id, u.Username, u.FullName, u.Email, u.SlackUserId,
        u.DepartmentId, u.Role, u.IsActive, u.CreatedAt, u.LastLoginAt);
}
