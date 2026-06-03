using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.BudgetRequests.Queries.ListBudgetRequests;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;

namespace SureBudgetRequest.Application.BudgetRequests.Queries.GetDashboard;

public sealed record GetDashboardQuery() : IRequest<Result<DashboardDto>>;

public sealed class GetDashboardQueryHandler
    : IRequestHandler<GetDashboardQuery, Result<DashboardDto>>
{
    // How many rows each card shows on the dashboard. Full lists live on the dedicated pages.
    private const int CardTopN = 5;

    // Threshold for "stuck" requests on the Admin view.
    private const int StuckDays = 7;

    private static readonly RequestStatus[] PendingStatuses =
    {
        RequestStatus.PendingDeptHead,
        RequestStatus.PendingManagement,
        RequestStatus.PendingFinance
    };

    private static readonly RequestStatus[] InFlightStatuses =
    {
        RequestStatus.PendingDeptHead,
        RequestStatus.PendingManagement,
        RequestStatus.PendingFinance,
        RequestStatus.Approved,
        RequestStatus.PartiallyPaid,
        RequestStatus.SentBack
    };

    private static readonly RequestStatus[] CompletedStatuses =
    {
        RequestStatus.Paid,
        RequestStatus.Rejected,
        RequestStatus.Cancelled
    };

    private static readonly RequestStatus[] ActionRequiredStatuses =
    {
        RequestStatus.Draft,
        RequestStatus.SentBack
    };

    private static readonly RequestStatus[] PaymentQueueStatuses =
    {
        RequestStatus.Approved,
        RequestStatus.PartiallyPaid
    };

    private readonly IBudgetRequestRepository _budgetRequestRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUser _currentUser;

    public GetDashboardQueryHandler(
        IBudgetRequestRepository budgetRequestRepository,
        IUserRepository userRepository,
        ICurrentUser currentUser)
    {
        _budgetRequestRepository = budgetRequestRepository;
        _userRepository = userRepository;
        _currentUser = currentUser;
    }

    public async Task<Result<DashboardDto>> Handle(
        GetDashboardQuery request,
        CancellationToken ct)
    {
        if (!_currentUser.IsLoaded)
            return Result.Failure<DashboardDto>(UserErrors.NoUserSignedIn);

        var userId = _currentUser.UserId;
        var role = _currentUser.Role;
        var departmentId = _currentUser.DepartmentId;

        // === Employee section (every role gets this) =========================

        var myActionRequired = await _budgetRequestRepository.ListAsync(
            requesterId: userId, statuses: ActionRequiredStatuses, cancellationToken: ct);

        var myInFlight = await _budgetRequestRepository.ListAsync(
            requesterId: userId, statuses: InFlightStatuses, cancellationToken: ct);

        var myCompleted = await _budgetRequestRepository.ListAsync(
            requesterId: userId, statuses: CompletedStatuses, cancellationToken: ct);

        var employee = new EmployeeSectionDto(
            ActionRequired: myActionRequired
                .OrderByDescending(r => r.CreatedAt)
                .Select(BudgetRequestSummaryDto.FromEntity)
                .ToList(),
            InFlight: myInFlight
                .OrderByDescending(r => r.SubmittedAt ?? r.CreatedAt)
                .Take(CardTopN)
                .Select(BudgetRequestSummaryDto.FromEntity)
                .ToList(),
            RecentlyCompleted: myCompleted
                .OrderByDescending(r => r.FinalizedAt ?? r.CreatedAt)
                .Take(CardTopN)
                .Select(BudgetRequestSummaryDto.FromEntity)
                .ToList());

        // === Role-specific: "Pending my approval" ===========================

        ApprovalQueueDto? myApprovalQueue = null;
        if (role is UserRole.DepartmentHead or UserRole.Management or UserRole.Finance)
        {
            var approvalStatus = role switch
            {
                UserRole.DepartmentHead => RequestStatus.PendingDeptHead,
                UserRole.Management     => RequestStatus.PendingManagement,
                UserRole.Finance        => RequestStatus.PendingFinance,
                _ => throw new InvalidOperationException()
            };

            Guid? deptFilter = role == UserRole.DepartmentHead ? departmentId : null;

            var pending = await _budgetRequestRepository.ListAsync(
                departmentId: deptFilter,
                status: approvalStatus,
                cancellationToken: ct);

            myApprovalQueue = new ApprovalQueueDto(
                TotalCount: pending.Count,
                TopItems: pending
                    .OrderBy(r => TypePriority(r.Type))                  // Urgent first
                    .ThenBy(r => r.SubmittedAt ?? r.CreatedAt)            // oldest first
                    .Take(CardTopN)
                    .Select(BudgetRequestSummaryDto.FromEntity)
                    .ToList());
        }

        // === Finance + Accounting: payment queue + metric tiles ==============
        // These are read-only tiles, so Accounting (read-only) sees them too.
        // The "Pending my approval" block above stays Finance-only — it's
        // action-framed and Accounting never approves.

        PaymentQueueDto? paymentQueue = null;
        FinanceMetricsDto? financeMetrics = null;
        if (role is UserRole.Finance or UserRole.Accounting)
        {
            var awaitingPayment = await _budgetRequestRepository.ListAsync(
                statuses: PaymentQueueStatuses, cancellationToken: ct);

            paymentQueue = new PaymentQueueDto(
                TotalCount: awaitingPayment.Count,
                TopItems: awaitingPayment
                    .OrderBy(r => r.FinalizedAt ?? r.SubmittedAt ?? r.CreatedAt) // oldest first
                    .Take(CardTopN)
                    .Select(BudgetRequestSummaryDto.FromEntity)
                    .ToList());

            // Metric: count waiting for Finance review
            var pendingFinanceCount = (await _budgetRequestRepository.ListAsync(
                status: RequestStatus.PendingFinance, cancellationToken: ct)).Count;

            // Metric: outstanding total in MMK (using each request's snapshot rate)
            // RemainingBalance is in CurrencyCode; convert with the stored ExchangeRateAtSubmission.
            var outstandingMmk = awaitingPayment
                .Sum(r => r.RemainingBalance * r.ExchangeRateAtSubmission);

            financeMetrics = new FinanceMetricsDto(
                PendingApprovalCount: pendingFinanceCount,
                AwaitingPaymentCount: awaitingPayment.Count,
                OutstandingTotalMmk: outstandingMmk);
        }

        // === Admin-only: stuck requests ====================================

        StuckRequestsDto? stuckRequests = null;
        if (role == UserRole.Admin)
        {
            var cutoff = DateTime.UtcNow.AddDays(-StuckDays);
            var pendingAll = await _budgetRequestRepository.ListAsync(
                statuses: PendingStatuses, cancellationToken: ct);

            var stuck = pendingAll
                .Where(r => (r.SubmittedAt ?? r.CreatedAt) <= cutoff)
                .OrderBy(r => r.SubmittedAt ?? r.CreatedAt)
                .ToList();

            stuckRequests = new StuckRequestsDto(
                TotalCount: stuck.Count,
                TopItems: stuck
                    .Take(CardTopN)
                    .Select(BudgetRequestSummaryDto.FromEntity)
                    .ToList());
        }

        // === Build the requester-name lookup =================================
        // Collect every distinct requester ID across all the lists we're returning,
        // then fetch their display names in one repository call.

        var allRequesterIds = new HashSet<Guid>();
        AddIds(allRequesterIds, myActionRequired);
        AddIds(allRequesterIds, myInFlight);
        AddIds(allRequesterIds, myCompleted);
        if (myApprovalQueue is not null) AddIds(allRequesterIds, myApprovalQueue.TopItems);
        if (paymentQueue is not null) AddIds(allRequesterIds, paymentQueue.TopItems);
        if (stuckRequests is not null) AddIds(allRequesterIds, stuckRequests.TopItems);

        var requesterNames = new Dictionary<Guid, string>();
        if (allRequesterIds.Count > 0)
        {
            var allUsers = await _userRepository.ListAsync(includeInactive: true, cancellationToken: ct);
            foreach (var u in allUsers)
            {
                if (allRequesterIds.Contains(u.Id))
                    requesterNames[u.Id] = u.FullName;
            }
        }

        var dashboard = new DashboardDto(
            Employee: employee,
            MyApprovalQueue: myApprovalQueue,
            PaymentQueue: paymentQueue,
            FinanceMetrics: financeMetrics,
            StuckRequests: stuckRequests,
            RequesterNames: requesterNames);

        return Result.Success(dashboard);
    }

    private static int TypePriority(BudgetRequestType t) => t switch
    {
        BudgetRequestType.Urgent          => 0,
        BudgetRequestType.Standard        => 1,
        BudgetRequestType.ProjectProposal => 2,
        _ => 3
    };

    private static void AddIds(HashSet<Guid> set, IEnumerable<BudgetRequest> items)
    {
        foreach (var r in items) set.Add(r.RequesterId);
    }

    private static void AddIds(HashSet<Guid> set, IEnumerable<BudgetRequestSummaryDto> items)
    {
        foreach (var r in items) set.Add(r.RequesterId);
    }
}
