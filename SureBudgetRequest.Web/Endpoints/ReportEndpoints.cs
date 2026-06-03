using System.Security.Claims;
using MediatR;
using SureBudgetRequest.Application.BudgetRequests.Queries.ExportBudgetRequests;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Web.Services;

namespace SureBudgetRequest.Web.Endpoints;

/// <summary>
/// HTTP endpoints for report exports. The export needs a real HTTP response so
/// the browser can save the generated <c>.xlsx</c>, so (like attachment
/// downloads) it lives here rather than on the SignalR circuit.
/// </summary>
public static class ReportEndpoints
{
    public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/reports/budget-requests/export
        // Streams an .xlsx of the budget-request report under the supplied filters.
        // Role scoping is re-enforced here so a crafted URL can't widen access.
        app.MapGet("/api/reports/budget-requests/export",
            async (
                IMediator mediator,
                HttpContext httpContext,
                CancellationToken ct,
                DateOnly? from,
                DateOnly? to,
                Guid? departmentId,
                Guid? coaId,
                string? currency,
                Guid? requesterId,
                Guid? approverId,
                string? overLimit,
                string[]? statuses) =>
            {
                // Identity is read from HttpContext.User (populated by cookie auth
                // on this HTTP request), NOT from ICurrentUser — that service is
                // backed by Blazor's circuit-scoped AuthenticationStateProvider and
                // throws when resolved outside a Razor component.
                var user = httpContext.User;
                var role = Enum.TryParse<UserRole>(user.FindFirstValue(ClaimTypes.Role), out var r)
                    ? r
                    : UserRole.Employee;

                // ── Server-side role scoping (security — do not skip) ───────────
                // Employees have no report access at all.
                if (role == UserRole.Employee)
                    return Results.Forbid();

                // Dept Heads are locked to their own department regardless of the
                // departmentId in the query string.
                if (role == UserRole.DepartmentHead)
                {
                    departmentId = Guid.TryParse(
                        user.FindFirstValue(CurrentUserService.ClaimDepartmentId), out var deptId)
                        ? deptId
                        : Guid.Empty;
                }

                // ── Date bounds — match BudgetRequests.razor exactly ────────────
                DateTime? fromUtc = from.HasValue
                    ? DateTime.SpecifyKind(from.Value.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc)
                    : null;
                DateTime? untilUtc = to.HasValue
                    ? DateTime.SpecifyKind(to.Value.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc).AddDays(1)
                    : null;

                bool? overLimitOnly = overLimit switch
                {
                    "over"   => true,
                    "within" => false,
                    _        => null
                };

                IReadOnlyCollection<RequestStatus>? statusFilter = null;
                if (statuses is { Length: > 0 })
                {
                    var parsed = statuses
                        .Select(s => Enum.TryParse<RequestStatus>(s, out var rs) ? rs : (RequestStatus?)null)
                        .Where(rs => rs.HasValue)
                        .Select(rs => rs!.Value)
                        .ToList();
                    if (parsed.Count > 0)
                        statusFilter = parsed;
                }

                var result = await mediator.Send(new ExportBudgetRequestsQuery(
                    RequesterId: requesterId,
                    DepartmentId: departmentId,
                    Statuses: statusFilter,
                    SubmittedFromUtc: fromUtc,
                    SubmittedUntilUtc: untilUtc,
                    CoaId: coaId,
                    CurrencyCode: string.IsNullOrEmpty(currency) ? null : currency,
                    ApproverId: approverId,
                    OverLimitOnly: overLimitOnly), ct);

                if (result.IsFailure || result.Value is null)
                    return Results.BadRequest(new { error = result.Error.Message ?? "Export failed." });

                var file = result.Value;
                return Results.File(file.Content, file.ContentType, file.FileName);
            })
            .RequireAuthorization()
            .WithName("ExportBudgetRequests");

        return app;
    }
}
