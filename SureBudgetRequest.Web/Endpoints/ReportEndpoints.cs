using System.Security.Claims;
using MediatR;
using SureBudgetRequest.Application.BudgetRequests.Queries.ExportBudgetRequests;
using SureBudgetRequest.Application.BudgetRequests.Queries.SearchBudgetRequests;
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
                string[]? statuses,
                string? search,
                string[]? types,
                decimal? amountFrom,
                decimal? amountTo,
                bool? periodOverrun,
                string? sortBy,
                bool? sortDesc,
                string[]? columns) =>
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

                // Type filter — same parse-and-drop-invalid shape as statuses.
                IReadOnlyCollection<BudgetRequestType>? typeFilter = null;
                if (types is { Length: > 0 })
                {
                    var parsedTypes = types
                        .Select(t => Enum.TryParse<BudgetRequestType>(t, out var bt) ? bt : (BudgetRequestType?)null)
                        .Where(bt => bt.HasValue)
                        .Select(bt => bt!.Value)
                        .ToList();
                    if (parsedTypes.Count > 0)
                        typeFilter = parsedTypes;
                }

                // Amount range — a reversed range (from > to) is ignored, exactly
                // as the report page does before running its on-screen query.
                if (amountFrom.HasValue && amountTo.HasValue && amountFrom.Value > amountTo.Value)
                {
                    amountFrom = null;
                    amountTo = null;
                }

                // Sort — falls back to the page's default (SubmittedAt desc) when
                // absent or unparsable.
                var sortColumn = Enum.TryParse<BudgetRequestSortBy>(sortBy, ignoreCase: true, out var sb)
                    ? sb
                    : BudgetRequestSortBy.SubmittedAt;

                // Column selection — ordered list of column keys. Order is
                // preserved (repeated query params keep their sequence), invalid
                // keys are dropped, and an empty result falls back to all columns
                // in the handler. Same parse-and-drop-invalid shape as statuses.
                IReadOnlyCollection<ExportColumn>? columnSelection = null;
                if (columns is { Length: > 0 })
                {
                    var parsedColumns = columns
                        .Select(c => Enum.TryParse<ExportColumn>(c, out var ec) ? ec : (ExportColumn?)null)
                        .Where(ec => ec.HasValue)
                        .Select(ec => ec!.Value)
                        .ToList();
                    if (parsedColumns.Count > 0)
                        columnSelection = parsedColumns;
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
                    OverLimitOnly: overLimitOnly,
                    Types: typeFilter,
                    AmountInMmkFrom: amountFrom,
                    AmountInMmkTo: amountTo,
                    PeriodOverrunOnly: periodOverrun == true ? true : null,
                    SearchTerm: string.IsNullOrWhiteSpace(search) ? null : search,
                    SortBy: sortColumn,
                    SortDescending: sortDesc ?? true,
                    Columns: columnSelection), ct);

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
