using SureBudgetRequest.Application.BudgetRequests.Common;

namespace SureBudgetRequest.Application.Abstractions.Services;

/// <summary>
/// Sends notifications (Slack in v1). Called after every status change.
/// Infrastructure implements this against the Slack API.
/// The Application layer only raises a <see cref="NotificationEvent"/> —
/// it has no knowledge of channels, tokens, or message formatting.
/// </summary>
public interface INotificationService
{
    Task SendAsync(NotificationEvent notification, CancellationToken cancellationToken = default);
}
