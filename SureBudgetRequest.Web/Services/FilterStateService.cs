namespace SureBudgetRequest.Web.Services;

using SureBudgetRequest.Application.BudgetRequests.Queries.SearchBudgetRequests;
using SureBudgetRequest.Domain.Enums;

/// <summary>
/// Circuit-scoped cache for preserving user filter and paging state across navigation within the same session.
/// Registered as Scoped in DI so it lives in server memory for the duration of the Blazor circuit.
/// </summary>
public sealed class FilterStateService
{
    public MyRequestsFilterState? MyRequests { get; set; }
    public InboxFilterState? Inbox { get; set; }
    public OutstandingPaymentsFilterState? OutstandingPayments { get; set; }
    public BudgetReportFilterState? BudgetReport { get; set; }
    public DashboardFilterState? Dashboard { get; set; }
    public UsersFilterState? Users { get; set; }
    public BudgetPlanFilterState? BudgetPlan { get; set; }

    private readonly Dictionary<string, string> _searches = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, bool> _flags = new(StringComparer.OrdinalIgnoreCase);

    public string? GetSearch(string key) => _searches.TryGetValue(key, out var val) ? val : null;

    public void SetSearch(string key, string? val)
    {
        if (string.IsNullOrWhiteSpace(val))
            _searches.Remove(key);
        else
            _searches[key] = val;
    }

    public bool? GetFlag(string key) => _flags.TryGetValue(key, out var val) ? val : null;

    public void SetFlag(string key, bool val) => _flags[key] = val;
}

public sealed class MyRequestsFilterState
{
    public string Search { get; set; } = "";
    public string SelectedTagId { get; set; } = "";
    public HashSet<RequestStatus> SelectedStatuses { get; set; } = [];
    public HashSet<BudgetRequestType> SelectedTypes { get; set; } = [];
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public BudgetRequestSortBy SortBy { get; set; } = BudgetRequestSortBy.SubmittedAt;
    public bool SortDescending { get; set; } = true;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public sealed class InboxFilterState
{
    public string Search { get; set; } = "";
    public string View { get; set; } = "inbox";
    public string SelectedDepartmentId { get; set; } = "";
    public string SelectedTagId { get; set; } = "";
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public decimal? AmountMmkFrom { get; set; }
    public decimal? AmountMmkTo { get; set; }
    public HashSet<BudgetRequestType> SelectedTypes { get; set; } = [];
    public BudgetRequestSortBy SortBy { get; set; } = BudgetRequestSortBy.SubmittedAt;
    public bool SortDescending { get; set; } = true;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public sealed class OutstandingPaymentsFilterState
{
    public string Search { get; set; } = "";
    public string SelectedDepartmentId { get; set; } = "";
    public string SelectedCurrencyCode { get; set; } = "";
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public HashSet<BudgetRequestType> SelectedTypes { get; set; } = [];
    public BudgetRequestSortBy SortBy { get; set; } = BudgetRequestSortBy.SubmittedAt;
    public bool SortDescending { get; set; } = true;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public sealed class BudgetReportFilterState
{
    public string Search { get; set; } = "";
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string SelectedDepartmentId { get; set; } = "";
    public string SelectedCoaId { get; set; } = "";
    public string SelectedTagId { get; set; } = "";
    public string SelectedCurrencyCode { get; set; } = "";
    public string SelectedWithdrawMethodId { get; set; } = "";
    public string SelectedRequesterId { get; set; } = "";
    public string SelectedApproverId { get; set; } = "";
    public string OverLimitFilter { get; set; } = "any";
    public HashSet<RequestStatus> SelectedStatuses { get; set; } = [];
    public HashSet<BudgetRequestType> SelectedTypes { get; set; } = [];
    public decimal? AmountMmkFrom { get; set; }
    public decimal? AmountMmkTo { get; set; }
    public bool PeriodOverrunOnly { get; set; }
    public BudgetRequestSortBy SortBy { get; set; } = BudgetRequestSortBy.SubmittedAt;
    public bool SortDescending { get; set; } = true;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public sealed class DashboardFilterState
{
    public string ActiveTab { get; set; } = "inflight";
    public int Page { get; set; } = 1;
    public string SortColumn { get; set; } = "submitted";
    public bool SortDescending { get; set; } = true;
}

public sealed class UsersFilterState
{
    public string Search { get; set; } = "";
    public string FilterDept { get; set; } = "";
    public string FilterRole { get; set; } = "";
    public bool ShowInactive { get; set; }
}

public sealed class BudgetPlanFilterState
{
    public int SelectedYear { get; set; }
    public Guid SelectedDeptId { get; set; }
}
