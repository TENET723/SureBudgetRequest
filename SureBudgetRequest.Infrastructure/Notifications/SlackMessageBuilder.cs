using System.Text.Json;
using SureBudgetRequest.Application.BudgetRequests.Common;
using SureBudgetRequest.Domain.Entities;

namespace SureBudgetRequest.Infrastructure.Notifications;

/// <summary>
/// Produces Slack messages in two shapes:
///
/// 1. SUBMISSION (the request enters the workflow) — full multi-line block.
///    The first approver needs to see the full request details to act on it.
///
/// 2. STATUS UPDATE (every transition after submission) — one-liner.
///    Intermediate approvals name who approved and who is waiting next.
///    Final outcomes (rejection, final approval, paid, sent back) tell
///    the requester the result.
///
/// The processor pre-loads the BudgetRequest and Department and resolves
/// the recipient @mentions, then hands everything here for formatting.
/// </summary>
internal static class SlackMessageBuilder
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    public static string Build(
        NotificationEvent evt,
        BudgetRequest request,
        string departmentName,
        IReadOnlyList<string> mentions)
    {
        var text = IsSubmissionTrigger(evt.Trigger)
            ? BuildSubmissionBody(evt, request, departmentName, mentions)
            : BuildStatusUpdateBody(evt, request, mentions);

        var payload = new
        {
            blocks = new[]
            {
                new
                {
                    type = "section",
                    text = new { type = "mrkdwn", text }
                }
            }
        };

        return JsonSerializer.Serialize(payload, JsonOpts);
    }

    // ---------------------------------------------------------------
    // Submission format — full request detail.
    // Used only for the three Submitted* triggers.
    // ---------------------------------------------------------------
    private static string BuildSubmissionBody(
        NotificationEvent evt,
        BudgetRequest request,
        string departmentName,
        IReadOnlyList<string> mentions)
    {
        // Comma-separated mentions like "<@U07ABC>, <@U07DEF>".
        var waitingForLine = string.Join(", ", mentions);

        var reference = request.Reference ?? "—";
        var requestType = request.Type.ToString();
        var submittedBy = string.IsNullOrWhiteSpace(evt.RequesterName) ? "—" : evt.RequesterName;
        var withdrawerName = request.WithdrawerName ?? string.Empty;
        var withdrawerTitle = request.WithdrawerJobTitle ?? string.Empty;
        var partialAcceptance = request.AllowsPartialPayment ? "Yes" : "No";
        var partialReasons = request.PartialPaymentDetail ?? string.Empty;
        var reasons = string.IsNullOrWhiteSpace(request.Reasons) ? string.Empty : request.Reasons;

        // Currency is MMK only in v1 but we read it off the request so it's
        // forward-compatible when multi-currency goes live.
        var amount = $"{request.RequestedAmount:N2} {request.CurrencyCode}";

        return string.Join("\n", new[]
        {
            $"ID: {reference}",
            $"Status: Pending",
            $"Request Type: {requestType}",
            $"Submitted By: {submittedBy}",
            $"Waiting For: {waitingForLine}",
            $"Department: {departmentName}",
            $"Withdraw Date: {request.RequestDate:yyyy-MM-dd}",
            $"Withdrawer Name: {withdrawerName}",
            $"Withdrawer Job Title: {withdrawerTitle}",
            $"Amount: {amount}",
            $"Reasons:",
            reasons,
            string.Empty,
            $"Partial Payment Acceptance: {partialAcceptance}",
            $"Partial Acceptance Reasons: {partialReasons}"
        });
    }

    // ---------------------------------------------------------------
    // Status-update format — short one-liner pinging whoever needs to
    // see this transition. Used for everything except submissions.
    // ---------------------------------------------------------------
    private static string BuildStatusUpdateBody(
        NotificationEvent evt,
        BudgetRequest request,
        IReadOnlyList<string> mentions)
    {
        var reference = request.Reference ?? "—";
        var actor = string.IsNullOrWhiteSpace(evt.ActorName) ? "—" : evt.ActorName;
        var mentionPrefix = mentions.Count > 0 ? string.Join(" ", mentions) + " " : string.Empty;

        var line = evt.Trigger switch
        {
            // Intermediate approvals — ping the next stage, name the previous approver.
            NotificationTrigger.DeptHeadApprovedToManagement
                => $"ID: {reference} is approved by {actor}. Waiting for Management approval. ✅",

            NotificationTrigger.DeptHeadApprovedToFinance
                => $"ID: {reference} is approved by {actor}. Waiting for Finance approval. ✅",

            NotificationTrigger.ManagementApprovedToFinance
                => $"ID: {reference} is approved by {actor}. Waiting for Finance approval. ✅",

            // Rejections — ping the requester.
            NotificationTrigger.DeptHeadRejectedToRequester
                => $"ID: {reference} was rejected by {actor}. ❌",

            NotificationTrigger.ManagementRejectedToRequester
                => $"ID: {reference} was rejected by {actor}. ❌",

            // Finance outcomes — ping the requester.
            NotificationTrigger.FinanceApprovedToRequester
                => $"ID: {reference} is approved by {actor}. ✅",

            NotificationTrigger.FinancePaidToRequester
                => $"ID: {reference} is marked as paid by {actor}. 💸",

            NotificationTrigger.FinanceSentBackToRequester
                => $"ID: {reference} was sent back by {actor} for revision. 🔄",

            // ── Advance withdrawal — reconciliation phase ──────────────────
            NotificationTrigger.AdvanceApprovedToRequester
                => $"ID: {reference} (advance) is approved by {actor}. ✅ "
                 + $"Reconcile your spending by {DeadlineText(request)} — "
                 + "record how the advance was used on the request page.",

            NotificationTrigger.ReconciliationSubmittedToFinance
                => $"ID: {reference}: reconciliation submitted by {actor}. 🧾",

            NotificationTrigger.AdvanceAwaitingRefund
                => $"ID: {reference}: reconciliation complete — a refund of "
                 + $"{RefundText(request)} is now due from the requester. 💵",

            NotificationTrigger.RefundRecordedToRequester
                => $"ID: {reference}: refund received and recorded by {actor}. "
                 + "The advance is now fully reconciled. ✅",

            _ => $"ID: {reference} status updated."
        };

        var message = $"{mentionPrefix}{line}";

        // Comment only applies to rejection / send-back.
        if (!string.IsNullOrWhiteSpace(evt.Comment))
            message += $"\nComment: {evt.Comment}";

        return message;
    }

    /// <summary>
    /// Only the three Submitted* triggers show the full request body.
    /// Everything else (approvals, rejections, paid, sent back, reconciliation)
    /// is a one-liner.
    /// </summary>
    private static bool IsSubmissionTrigger(NotificationTrigger trigger) => trigger is
        NotificationTrigger.SubmittedToDeptHead or
        NotificationTrigger.SubmittedToFinance or
        NotificationTrigger.SubmittedToManagement;

    private static string DeadlineText(BudgetRequest request) =>
        request.ReconciliationDeadline.HasValue
            ? request.ReconciliationDeadline.Value.ToString("yyyy-MM-dd")
            : "—";

    private static string RefundText(BudgetRequest request) =>
        $"{request.RefundAmount:N2} {request.CurrencyCode}";
}
