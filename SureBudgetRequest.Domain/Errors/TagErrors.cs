using SureBudgetRequest.Domain.Common;

namespace SureBudgetRequest.Domain.Errors;

public static class TagErrors
{
    public static readonly Error NotFound =
        Error.NotFound("Tag.NotFound", "Tag not found.");

    public static Error AlreadyExists(string name) =>
        Error.Conflict("Tag.AlreadyExists", $"Tag '{name}' already exists in this department.");

    public static Error Inactive(string name) =>
        Error.Validation("Tag.Inactive", $"Tag '{name}' is deactivated and cannot be used.");

    public static readonly Error CrossDepartmentNotAllowed =
        Error.Forbidden("Tag.CrossDepartmentNotAllowed", "You can only attach tags belonging to your own department.");

    public static readonly Error CannotRemoveCrossDepartmentTag =
        Error.Forbidden("Tag.CannotRemoveCrossDepartmentTag", "You cannot remove a tag belonging to another department.");

    public static Error AlreadyAttached(string name) =>
        Error.Conflict("Tag.AlreadyAttached", $"Tag '{name}' is already attached to this request.");

    public static readonly Error TagNotAttached =
        Error.NotFound("Tag.NotAttached", "The specified tag is not attached to this request.");

    public static readonly Error CannotAttachTag =
        Error.Forbidden("Tag.CannotAttachTag", "You do not have permission to attach tags to this request.");

    public static readonly Error CannotRemoveTag =
        Error.Forbidden("Tag.CannotRemoveTag", "You do not have permission to remove tags from this request.");

    public static Error MaxDepartmentTagsReached(int max) =>
        Error.Validation("Tag.MaxLimitReached", $"Your department cannot attach more than {max} tags to this request.");

    public static Error ValidationError(string message) =>
        Error.Validation("Tag.Validation", message);
}
