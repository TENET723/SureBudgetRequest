using MediatR;
using SureBudgetRequest.Application.Abstractions.Services;
using SureBudgetRequest.Application.BudgetRequests.Queries.ListBudgetRequests;
using SureBudgetRequest.Application.BudgetRequests.Queries.SearchBudgetRequests;
using SureBudgetRequest.Application.Departments.Queries;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Enums;

namespace SureBudgetRequest.Application.BudgetRequests.Queries.ExportBudgetRequests;

/// <summary>
/// Exports the currently-filtered budget-request list to an <c>.xlsx</c> file.
/// Takes the same filter parameters as <see cref="ListBudgetRequestsQuery"/> and
/// reuses it (via <see cref="IMediator"/>) so no filtering / COA-resolution
/// logic is duplicated. The caller (Web endpoint) is responsible for role-based
/// scoping before sending this query.
///
/// Carries EVERY filter dimension the report page's on-screen query
/// (<c>SearchBudgetRequestsQuery</c>) supports, plus the sort selection — the
/// export must contain exactly the rows the user is looking at, in the same
/// order. When adding a filter to the report page, add it here too.
/// </summary>
public sealed record ExportBudgetRequestsQuery(
    Guid? RequesterId = null,
    Guid? DepartmentId = null,
    IReadOnlyCollection<RequestStatus>? Statuses = null,
    DateTime? SubmittedFromUtc = null,
    DateTime? SubmittedUntilUtc = null,
    Guid? CoaId = null,
    string? CurrencyCode = null,
    Guid? ApproverId = null,
    bool? OverLimitOnly = null,
    IReadOnlyCollection<BudgetRequestType>? Types = null,
    decimal? AmountInMmkFrom = null,
    decimal? AmountInMmkTo = null,
    bool? PeriodOverrunOnly = null,
    string? SearchTerm = null,
    BudgetRequestSortBy SortBy = BudgetRequestSortBy.SubmittedAt,
    bool SortDescending = true) : IRequest<Result<FileDownload>>;

public sealed class ExportBudgetRequestsQueryHandler
    : IRequestHandler<ExportBudgetRequestsQuery, Result<FileDownload>>
{
    private readonly IMediator _mediator;
    private readonly IReportExporter _exporter;

    public ExportBudgetRequestsQueryHandler(IMediator mediator, IReportExporter exporter)
    {
        _mediator = mediator;
        _exporter = exporter;
    }

    public async Task<Result<FileDownload>> Handle(
        ExportBudgetRequestsQuery request,
        CancellationToken cancellationToken)
    {
        // Reuse the existing list query so filtering / COA resolution lives in
        // exactly one place. Every filter the report page applies on screen is
        // forwarded here — see the record's doc comment.
        var listResult = await _mediator.Send(new ListBudgetRequestsQuery(
            RequesterId: request.RequesterId,
            DepartmentId: request.DepartmentId,
            Status: null,
            Statuses: request.Statuses,
            SubmittedFromUtc: request.SubmittedFromUtc,
            SubmittedUntilUtc: request.SubmittedUntilUtc,
            CoaId: request.CoaId,
            CurrencyCode: request.CurrencyCode,
            ApproverId: request.ApproverId,
            OverLimitOnly: request.OverLimitOnly,
            Types: request.Types,
            AmountInMmkFrom: request.AmountInMmkFrom,
            AmountInMmkTo: request.AmountInMmkTo,
            PeriodOverrunOnly: request.PeriodOverrunOnly,
            SearchTerm: request.SearchTerm), cancellationToken);

        if (listResult.IsFailure || listResult.Value is null)
            return Result<FileDownload>.Failure(listResult.Error);

        // Resolve department display names. Include inactive so historical rows
        // referencing deactivated departments still resolve — matches how
        // BudgetRequests.razor builds its name lookup. (Requester names come
        // from the RequesterNameAtSubmission snapshot on the DTO, same as the
        // UI, so no users lookup is needed.)
        var deptsResult = await _mediator.Send(new ListDepartmentsQuery(IncludeInactive: true), cancellationToken);

        var departmentNames = deptsResult.IsSuccess && deptsResult.Value is not null
            ? deptsResult.Value.ToDictionary(d => d.Id, d => d.Name)
            : new Dictionary<Guid, string>();

        var rows = ApplySort(listResult.Value, request.SortBy, request.SortDescending)
            .Select(r => new BudgetRequestExportRow(
                Reference: r.Reference,
                SubmittedAt: r.SubmittedAt,
                RequesterName: r.RequesterName,
                DepartmentName: departmentNames.TryGetValue(r.DepartmentIdAtSubmission, out var dept) ? dept : "—",
                Reason: r.Reasons,
                CurrencyCode: r.CurrencyCode,
                RequestedAmount: r.RequestedAmount,
                AmountInMmkAtSubmission: r.RequestedAmountInMmkAtSubmission,
                StatusLabel: StatusLabel(r.Status),
                IsOverLimit: r.IsOverLimit,
                CoaCode: r.CoaCode,
                CoaName: r.CoaName,
                WithdrawMethodName: r.WithdrawMethodName))
            .ToList();

        var bytes = _exporter.ExportBudgetRequests(rows);

        return Result<FileDownload>.Success(new FileDownload(
            bytes,
            $"budget-requests_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"));
    }

    /// <summary>
    /// In-memory sort over the already-filtered DTOs, mirroring the column
    /// semantics of <c>BudgetRequestRepository.ApplySort</c> (including the
    /// CreatedAt-descending tiebreaker) so the export rows come out in the same
    /// order the report page displays them.
    /// </summary>
    private static IEnumerable<BudgetRequestSummaryDto> ApplySort(
        IReadOnlyList<BudgetRequestSummaryDto> items, BudgetRequestSortBy sortBy, bool descending)
    {
        IOrderedEnumerable<BudgetRequestSummaryDto> ordered = sortBy switch
        {
            BudgetRequestSortBy.RequestDate => descending
                ? items.OrderByDescending(r => r.RequestDate)
                : items.OrderBy(r => r.RequestDate),
            BudgetRequestSortBy.Reference => descending
                ? items.OrderByDescending(r => r.Reference, StringComparer.Ordinal)
                : items.OrderBy(r => r.Reference, StringComparer.Ordinal),
            BudgetRequestSortBy.Requester => descending
                ? items.OrderByDescending(r => r.RequesterName, StringComparer.OrdinalIgnoreCase)
                : items.OrderBy(r => r.RequesterName, StringComparer.OrdinalIgnoreCase),
            BudgetRequestSortBy.Type => descending
                ? items.OrderByDescending(r => r.Type)
                : items.OrderBy(r => r.Type),
            BudgetRequestSortBy.AmountInMmk => descending
                ? items.OrderByDescending(r => r.RequestedAmountInMmkAtSubmission)
                : items.OrderBy(r => r.RequestedAmountInMmkAtSubmission),
            BudgetRequestSortBy.Status => descending
                ? items.OrderByDescending(r => r.Status)
                : items.OrderBy(r => r.Status),
            BudgetRequestSortBy.OutstandingSince => descending
                ? items.OrderByDescending(r => r.FinalizedAt ?? r.SubmittedAt ?? r.CreatedAt)
                : items.OrderBy(r => r.FinalizedAt ?? r.SubmittedAt ?? r.CreatedAt),
            BudgetRequestSortBy.ReconciliationDeadline => descending
                ? items.OrderByDescending(r => r.ReconciliationDeadline)
                : items.OrderBy(r => r.ReconciliationDeadline),
            // SubmittedAt is the default.
            _ => descending
                ? items.OrderByDescending(r => r.SubmittedAt)
                : items.OrderBy(r => r.SubmittedAt),
        };

        return ordered.ThenByDescending(r => r.CreatedAt);
    }

    /// <summary>
    /// Human-friendly status label. Kept here (Application layer) and in sync
    /// with the page's <c>StatusLabel</c> switch so the Infrastructure exporter
    /// stays a pure formatter.
    /// </summary>
    private static string StatusLabel(RequestStatus s) => s switch
    {
        RequestStatus.Draft             => "Draft",
        RequestStatus.PendingDeptHead   => "Pending Head",
        RequestStatus.PendingManagement => "Pending Mgmt",
        RequestStatus.PendingFinance    => "Pending Finance",
        RequestStatus.SentBack          => "Sent Back",
        RequestStatus.Approved          => "Approved",
        RequestStatus.PartiallyPaid     => "Part. Paid",
        RequestStatus.Paid              => "Paid",
        RequestStatus.Rejected          => "Rejected",
        RequestStatus.Cancelled         => "Cancelled",
        _                               => s.ToString()
    };
}
