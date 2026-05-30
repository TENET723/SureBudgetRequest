namespace SureBudgetRequest.Domain.Entities;

/// <summary>
/// Audit row written when a BudgetRequest is modified.
/// Created by the Application layer in the same transaction as the update.
/// </summary>
public class BudgetRequestModification
{
    public Guid Id { get; private set; }
    public Guid BudgetRequestId { get; private set; }
    public Guid ModifiedByUserId { get; private set; }
    public DateTime ModifiedAt { get; private set; }
    public string? Note { get; private set; }

    // For EF Core
    private BudgetRequestModification() { }

    public BudgetRequestModification(Guid budgetRequestId, Guid modifiedByUserId, string? note = null)
    {
        if (budgetRequestId == Guid.Empty)
            throw new ArgumentException("Budget request ID is required.", nameof(budgetRequestId));
        if (modifiedByUserId == Guid.Empty)
            throw new ArgumentException("Modified by user ID is required.", nameof(modifiedByUserId));

        BudgetRequestId = budgetRequestId;
        ModifiedByUserId = modifiedByUserId;
        ModifiedAt = DateTime.UtcNow;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }
}
