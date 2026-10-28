using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Errors;

namespace SureBudgetRequest.Domain.Entities;

public partial class BudgetRequest
{
    /// <summary>
    /// Attaches a department tag to the budget request.
    /// Invariants:
    ///   - The tag must be active.
    ///   - The user must belong to the department that owns the tag.
    ///   - The tag cannot already be attached.
    ///   - The user's department cannot exceed the max tags limit on this request.
    /// </summary>
    public Result AddTag(
        Tag tag,
        Guid attachedByUserId,
        Guid userDepartmentId,
        int maxTagsPerDepartment = 5)
    {
        if (tag is null)
            throw new ArgumentNullException(nameof(tag));

        if (!tag.IsActive)
            return Result.Failure(TagErrors.Inactive(tag.Name));

        if (userDepartmentId != tag.DepartmentId)
            return Result.Failure(TagErrors.CrossDepartmentNotAllowed);

        if (_tags.Any(t => t.TagId == tag.Id))
            return Result.Failure(TagErrors.AlreadyAttached(tag.Name));

        var currentDeptTagCount = _tags.Count(t => t.TagDepartmentId == tag.DepartmentId);
        if (currentDeptTagCount >= maxTagsPerDepartment)
            return Result.Failure(TagErrors.MaxDepartmentTagsReached(maxTagsPerDepartment));

        _tags.Add(new BudgetRequestTag(Id, tag.Id, tag.DepartmentId, attachedByUserId, tag));
        return Result.Success();
    }

    /// <summary>
    /// Removes a tag from the budget request.
    /// Invariant:
    ///   - A user can only remove tags belonging to their own department.
    /// </summary>
    public Result RemoveTag(
        Guid tagId,
        Guid removedByUserId,
        Guid userDepartmentId)
    {
        var existing = _tags.FirstOrDefault(t => t.TagId == tagId);
        if (existing is null)
            return Result.Failure(TagErrors.TagNotAttached);

        if (userDepartmentId != existing.TagDepartmentId)
            return Result.Failure(TagErrors.CannotRemoveCrossDepartmentTag);

        _tags.Remove(existing);
        return Result.Success();
    }
}
