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
