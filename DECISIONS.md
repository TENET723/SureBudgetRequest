# Architectural & Domain Decisions Record

This document records key architectural, schema, security, and domain workflow decisions made in the `SureBudgetRequest` project.

---

## ADR-001: Budget Request Accounting Categorization & Role Boundary

- **Date**: 2026-11-17
- **Status**: Accepted
- **Context**:
  Budget requests require accounting classification to group expenditures for reporting, audit, and ledger mapping. Initially, categorization, master category management, and queue views were considered across Finance, Admin, and Accounting.
- **Decision**:
  1. **Strict Role Isolation for Categorization Actions**:
     - Master category management (`/accounting-categories`), category creation, modification, and deactivation are strictly restricted to `UserRole.Accounting`.
     - The "To Categorize" homepage queue tab and badge are exclusively populated for and visible to `UserRole.Accounting`.
     - Assigning or updating an accounting category (via quick context menu or detail modal) is authorized exclusively for `UserRole.Accounting`.
     - Non-Accounting roles attempting to call `AssignAccountingCategoryCommand` receive a `BudgetRequestErrors.OnlyAccountingCanCategorize` forbidden error.
  2. **Global Read-Only Visibility**:
     - The assigned accounting category name is visible to all users across the system, including on the homepage dashboard and the budget request list report.
- **Rationale**:
  Keeps accounting classification segregated as an accounting-specific responsibility, preventing accidental modification by Finance, Department Heads, or Administrators, while keeping ticket classification status transparent to all stakeholders.

---

## ADR-002: Dual-Mode Modal View for Budget Request Details

- **Date**: 2026-11-19
- **Status**: Accepted
- **Context**:
  Viewing a budget request detail previously navigated away from list pages (`/requests/mine`, `/requests/inbox`, `/requests/outstanding`, `/reports/budget-requests`) to `/requests/{id}`. Upon returning to the list, the list component re-mounted, resetting pagination, search criteria, and scroll position, and triggering an expensive redundant API query against the database.
- **Decision**:
  1. **In-Memory Full Modal Overlay (`RequestDetailModal`)**:
     - Clicking a table row or context menu item in list views opens the request detail inside a centered macOS-style modal dialog (`RequestDetailModal.razor`) overlaid on the active list.
     - Closing the modal resets the selected ID in-memory without firing any list API requests, preserving 100% of the user's active page, scroll position, search query, and applied filters.
  2. **In-Place List Refresh on Action**:
     - When an action is taken inside the modal (e.g., Approve, Reject, Void, Record Payment), an `OnRequestUpdated` event callback triggers a targeted in-place page reload on the list without resetting pagination or scroll.
  3. **Dual-Mode Component Compatibility**:
     - `RequestDetail.razor` supports both `IsModal="true"` (for modal overlay) and `IsModal="false"` (for standalone page routing at `/requests/{id}`), ensuring external deep links, bookmarks, notifications, and new-tab views remain fully functional.
- **Rationale**:
  Radically reduces latency and database load during routine request review and approval workflows, completely eliminates user friction from lost scroll/filter states, and modernizes the application's UX.
