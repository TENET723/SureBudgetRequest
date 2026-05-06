using System.Text.Json;
using SureBudgetRequest.Application.BudgetRequests.Common;

namespace SureBudgetRequest.Infrastructure.Notifications;

/// <summary>
/// Converts a <see cref="NotificationEvent"/> into a Slack Block Kit JSON payload.
/// Keep all message copy here so it's easy to find and translate later.
/// </summary>
internal static class SlackMessageBuilder
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    public static string Build(NotificationEvent evt)
    {
        var (emoji, headline, detail) = GetMessageParts(evt);

        var blocks = new object[]
        {
            new
            {
                type = "header",
                text = new { type = "plain_text", text = $"{emoji} {headline}", emoji = true }
            },
            new
            {
                type = "section",
                fields = new[]
                {
                    new { type = "mrkdwn", text = $"*Request:*\n{evt.BudgetRequestTitle}" },
                    new { type = "mrkdwn", text = $"*Amount:*\n{evt.RequestedAmount:N0} MMK" }
                }
            },
            new
            {
                type = "section",
                text = new { type = "mrkdwn", text = detail }
            }
        };

        // Append comment block only if one was provided (rejections / send-backs)
        object[] finalBlocks = string.IsNullOrWhiteSpace(evt.Comment)
            ? blocks
            :
            [
                ..blocks,
                new
                {
                    type = "section",
                    text = new { type = "mrkdwn", text = $"*Comment:*\n{evt.Comment}" }
                }
            ];

        return JsonSerializer.Serialize(new { blocks = finalBlocks }, JsonOpts);
    }

    private static (string emoji, string headline, string detail) GetMessageParts(NotificationEvent evt)
        => evt.Trigger switch
        {
            NotificationTrigger.SubmittedToDeptHead =>
                ("📋", "New budget request awaiting your approval",
                 "A new request has been submitted and is waiting for Department Head approval."),

            NotificationTrigger.SubmittedToFinance =>
                ("📋", "New budget request awaiting Finance approval",
                 "A Department Head submitted their own request (under limit) — it goes directly to Finance."),

            NotificationTrigger.SubmittedToBoss =>
                ("📋", "New over-limit request awaiting your approval",
                 "A Department Head submitted an over-limit request. Boss approval is required."),

            NotificationTrigger.DeptHeadApprovedToBoss =>
                ("✅", "Department Head approved — Boss approval needed",
                 "The request was approved by the Department Head and now awaits Boss approval."),

            NotificationTrigger.DeptHeadApprovedToFinance =>
                ("✅", "Department Head approved — Finance action needed",
                 "The request was approved by the Department Head and is now with Finance."),

            NotificationTrigger.DeptHeadRejectedToRequester =>
                ("❌", "Your budget request was rejected",
                 "The Department Head has rejected your request."),

            NotificationTrigger.BossApprovedToFinance =>
                ("✅", "Boss approved — Finance action needed",
                 "The over-limit request was approved by the Boss and is now with Finance."),

            NotificationTrigger.BossRejectedToRequester =>
                ("❌", "Your budget request was rejected",
                 "The Boss has rejected your request."),

            NotificationTrigger.FinanceApprovedToRequester =>
                ("🎉", "Your budget request was approved!",
                 "Finance has approved your request. Payment will be processed shortly."),

            NotificationTrigger.FinancePaidToRequester =>
                ("💸", "Payment has been recorded",
                 "Finance has marked your request as paid."),

            NotificationTrigger.FinanceSentBackToRequester =>
                ("🔄", "Your budget request was sent back for revision",
                 "Finance has sent back your request. Please review the comment, edit your draft, and resubmit."),

            _ => ("ℹ️", "Budget request update", "Your budget request status has changed.")
        };
}
