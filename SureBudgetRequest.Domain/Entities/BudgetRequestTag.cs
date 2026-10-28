namespace SureBudgetRequest.Domain.Entities;

/// <summary>
/// Join entity associating a <see cref="Tag"/> with a <see cref="BudgetRequest"/>.
/// Maintains a snapshot of the tag's department for fast per-department limit
/// checks and visibility filtering.
/// </summary>
public class BudgetRequestTag
{
    public Guid Id { get; private set; }
    public Guid BudgetRequestId { get; private set; }
    public Guid TagId { get; private set; }
    public Tag? Tag { get; private set; }
    public Guid TagDepartmentId { get; private set; }
    public Guid AttachedByUserId { get; private set; }
    public DateTime AttachedAt { get; private set; }

    // For EF Core
    private BudgetRequestTag() { }

    internal BudgetRequestTag(
        Guid budgetRequestId,
        Guid tagId,
        Guid tagDepartmentId,
        Guid attachedByUserId,
        Tag? tag = null)
    {
        if (budgetRequestId == Guid.Empty)
            throw new ArgumentException("Budget request ID is required.", nameof(budgetRequestId));
        if (tagId == Guid.Empty)
            throw new ArgumentException("Tag ID is required.", nameof(tagId));
        if (tagDepartmentId == Guid.Empty)
            throw new ArgumentException("Tag department ID is required.", nameof(tagDepartmentId));
        if (attachedByUserId == Guid.Empty)
            throw new ArgumentException("Attached by user ID is required.", nameof(attachedByUserId));

        //Id = Guid.NewGuid();
        BudgetRequestId = budgetRequestId;
        TagId = tagId;
        TagDepartmentId = tagDepartmentId;
        AttachedByUserId = attachedByUserId;
        AttachedAt = DateTime.UtcNow;
        Tag = tag;
    }
}
