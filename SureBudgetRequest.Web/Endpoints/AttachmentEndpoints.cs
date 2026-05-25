using System.Net;
using MediatR;
using SureBudgetRequest.Application.BudgetRequests.Queries.DownloadAttachment;

namespace SureBudgetRequest.Web.Endpoints;

/// <summary>
/// HTTP endpoints for attachment downloads. Upload and remove are handled
/// directly from the Blazor components via MediatR — they don't need to leave
/// the SignalR circuit. Downloads need a real HTTP response so the browser
/// can save / preview the file, so they live here.
/// </summary>
public static class AttachmentEndpoints
{
    public static IEndpointRouteBuilder MapAttachmentEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/attachments/{attachmentId}
        // Streams the file with Content-Disposition. Inline display for PDFs and images,
        // "attachment" (force download) for everything else.
        app.MapGet("/api/attachments/{attachmentId:guid}",
            async (
                Guid attachmentId,
                IMediator mediator,
                HttpContext httpContext,
                CancellationToken ct) =>
            {
                var result = await mediator.Send(new DownloadAttachmentQuery(attachmentId), ct);
                if (result.IsFailure || result.Value is null)
                {
                    return Results.NotFound(new { error = result.Error.Message ?? "Attachment not found." });
                }

                var content = result.Value;
                var disposition = IsInlineFriendly(content.ContentType) ? "inline" : "attachment";
                var encodedName = WebUtility.UrlEncode(content.FileName);

                httpContext.Response.Headers["Content-Disposition"] =
                    $"{disposition}; filename*=UTF-8''{encodedName}";

                return Results.Stream(content.Stream, contentType: content.ContentType);
            })
            .RequireAuthorization()
            .WithName("DownloadAttachment");

        return app;
    }

    private static bool IsInlineFriendly(string contentType) =>
        contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(contentType, "application/pdf", StringComparison.OrdinalIgnoreCase) ||
        contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase);
}
