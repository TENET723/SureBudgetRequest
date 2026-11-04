using SureBudgetRequest.Application.Abstractions.Services;

namespace SureBudgetRequest.Web.Services;

/// <summary>
/// In-memory broadcast service to immediately notify active Blazor circuits
/// when a user's leave delegation is revoked.
/// </summary>
public sealed class DelegationRevocationNotifier : IDelegationRevocationNotifier
{
    public event Action<Guid>? OnRevocation;

    public void NotifyRevocation(Guid targetUserId)
    {
        OnRevocation?.Invoke(targetUserId);
    }
}
