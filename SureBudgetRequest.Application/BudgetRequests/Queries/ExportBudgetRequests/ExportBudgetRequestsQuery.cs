using MediatR;
using SureBudgetRequest.Application.Abstractions.Services;
using SureBudgetRequest.Application.BudgetRequests.Queries.ListBudgetRequests;
using SureBudgetRequest.Application.Departments.Queries;
using SureBudgetRequest.Application.Users.Queries;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Enums;

namespace SureBudgetRequest.Application.BudgetRequests.Queries.ExportBudgetRequests;

/// <summary>
/// Exports the currently-filtered budget-request list to an <c>.xlsx</c> file.
/// Takes the same filter parameters as <see cref="ListBudgetRequestsQuery"/> and
/// reuses it (via <see cref="IMediator"/>) so no filtering / COA-resolution
/// logic is duplicated. The caller (Web endpoint) is responsible for role-based
/// scoping before sending this query.
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
    bool? OverLimitOnly = null) : IRequest<Result<FileDownload>>;

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
        // exactly one place.
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
            OverLimitOnly: request.OverLimitOnly), cancellationToken);

        if (listResult.IsFailure || listResult.Value is null)
            return Result<FileDownload>.Failure(listResult.Error);

        // Resolve requester / department display names. Include inactive so
        // historical rows referencing deactivated users or departments still
        // resolve — matches how BudgetRequests.razor builds its name lookups.
        var usersResult = await _mediator.Send(new ListUsersQuery(IncludeInactive: true), cancellationToken);
        var deptsResult = await _mediator.Send(new ListDepartmentsQuery(IncludeInactive: true), cancellationToken);

        var userNames = usersResult.IsSuccess && usersResult.Value is not null
            ? usersResult.Value.ToDictionary(u => u.Id, u => u.FullName)
            : new Dictionary<Guid, string>();
        var departmentNames = deptsResult.IsSuccess && deptsResult.Value is not null
            ? deptsResult.Value.ToDictionary(d => d.Id, d => d.Name)
            : new Dictionary<Guid, string>();

        var rows = listResult.Value
            .OrderByDescending(r => r.SubmittedAt ?? r.CreatedAt)
            .Select(r => new BudgetRequestExportRow(
                Reference: r.Reference,
                SubmittedAt: r.SubmittedAt,
                RequesterName: userNames.TryGetValue(r.RequesterId, out var requester) ? requester : "—",
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
