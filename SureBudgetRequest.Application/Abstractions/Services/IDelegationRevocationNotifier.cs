namespace SureBudgetRequest.Application.Abstractions.Services;

/// <summary>
/// Notifies active sessions when a user's leave delegation is revoked or set to false.
/// </summary>
public interface IDelegationRevocationNotifier
{
    /// <summary>
    /// Event fired when a target user's leave status is turned off or fallback delegation is revoked.
    /// The parameter is the target user ID whose delegation was revoked.
    /// </summary>
    event Action<Guid>? OnRevocation;

    /// <summary>
    /// Broadcasts that delegation for <paramref name="targetUserId"/> is revoked.
    /// </summary>
    void NotifyRevocation(Guid targetUserId);
}
