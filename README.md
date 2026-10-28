# SureBudgetRequest

Internal budget-request web application for **aSure** (Myanmar). Employees submit budget requests that progress through a multi-stage approval workflow:
`Requester (Draft)` → `Department Head` → `Management` (if over department limit) → `Finance` → `Payment (Single or Installments)` → `Reconciliation` (for Advance requests).

---

## Architecture & Technology Stack

- **Platform**: .NET 9 (`net9.0`)
- **UI**: Blazor Server SSR with Interactive Server Components
- **Architecture**: Clean Architecture / Domain-Driven Design (DDD) with CQRS (MediatR 12)
- **Database**: PostgreSQL (Supabase) via Entity Framework Core 9 (snake_case naming conventions)
- **Security**: Cookie Authentication with PBKDF2-SHA256 password hashing and role-based authorization
- **Unit Testing**: xUnit, FluentAssertions, NSubstitute (1,062 automated unit tests across Domain, Application, and Infrastructure - 100% passing)

---

## Solution Structure

```text
SureBudgetRequest.slnx
├── SureBudgetRequest.Domain/                # Pure domain model, entities, enums, value objects, domain errors
├── SureBudgetRequest.Domain.Tests/          # 410 domain unit tests (100% passing)
├── SureBudgetRequest.Application/           # CQRS commands, queries, validators, service abstractions
├── SureBudgetRequest.Application.Tests/     # 622 application unit tests (100% passing)
├── SureBudgetRequest.Infrastructure/        # EF Core, persistence, security, file storage, ClosedXML
├── SureBudgetRequest.Infrastructure.Tests/  # 30 infrastructure unit tests (100% passing)
└── SureBudgetRequest.Web/                   # Blazor UI, components, authentication, DI composition root
```

---

## Feature Specification: Department-Isolated Tags for Tickets

### 1. Overview & Business Intent
Tags provide cross-cutting labels and categorization for budget requests (e.g., `#urgent`, `#capex-2026`, `#tax-audit`, `#q4-campaign`) without altering the formal Chart of Accounts (COA) or single Budget Category.

To keep department terminology clean and avoid clutter across unrelated teams:
- Tags are **isolated at the department level**.
- Tags created within a department are shared among all members of that department.
- When cross-department stakeholders (Finance, Management, Dept Heads) review tickets, their visibility and tagging rights follow strict governance rules.

---

### 2. Domain Data Model

#### A. `Tag` (Entity)
Represents a reusable tag definition belonging to a specific department.
* **`Id`** (`Guid`, Primary Key)
* **`DepartmentId`** (`Guid`, Foreign Key to `Department`)
* **`Name`** (`string`, max 50 chars, trimmed)
  * **Uniqueness Rule**: Case-insensitive unique per department (`UNIQUE(department_id, LOWER(name))`). Different departments may independently use identical names (e.g., both IT and Marketing can have `#vendor`).
* **`ColorHex`** (`string?`, e.g., `#3B82F6` for visual UI chip rendering)
* **`IsActive`** (`bool`, default `true` — soft-deactivatable)
* **`CreatedByUserId`** (`Guid`)
* **`CreatedAt`** (`DateTime`, UTC)

#### B. `BudgetRequestTag` (Aggregate Child Entity / Join)
Associates a `Tag` with a `BudgetRequest`.
* **`BudgetRequestId`** (`Guid`, Foreign Key to `BudgetRequest`)
* **`TagId`** (`Guid`, Foreign Key to `Tag`)
* **`DepartmentId`** (`Guid`, snapshotted department of the tag for efficient per-department limit checks and visibility queries)
* **`AttachedByUserId`** (`Guid`)
* **`AttachedAt`** (`DateTime`, UTC)

---

### 3. Permissions & Governance Rules

| Action | Allowed Roles / Users | Rules & Constraints |
| :--- | :--- | :--- |
| **Create New Tag** | Any active user in Department X, or System Admin | Department members can only create tags for their own department (`User.DepartmentId == Tag.DepartmentId`). System Admins can create tags across any department. Supports custom background (`ColorHex`) and font (`FontColorHex`) with automatic high-contrast calculation. |
| **Update / Rename Tag** | Any active user in Department X, or System Admin | Department members can rename and change background/font colors within their own department. |
| **Deactivate / Reactivate Tag** | Any active user in Department X, or System Admin | Deactivating a tag preserves historical tags on existing tickets, but excludes it from new autocomplete dropdowns. |
| **Attach Tag to Ticket** | Requester, Department Head, Management, Finance | **Strict Own-Department Rule**: Users may **only** attach tags belonging to their own department. |
| **Remove Tag from Ticket** | Any user belonging to the department that owns the tag | **Strict Deletion Boundary**: Users can **never** remove a tag attached by another department (e.g., IT cannot remove Finance's audit tag). |
| **Ticket Status Gate** | Any status (`Draft`, `Pending*`, `Approved`, `Paid`, `Reconciled`) | Tags act like comments/labels; they can be freely added or removed across the entire lifecycle, including after payment. |

---

### 4. Cross-Department Visibility & Search Scope

#### A. Visibility Scope Matrix
Cross-department visibility on ticket details is configurable by System Admin (stored as structured JSON in `AppSetting` with key `TagSettings:Config`):
```json
{
  "FinanceDepartmentId": "00000000-0000-0000-0001-000000000003",
  "ManagementDepartmentId": "00000000-0000-0000-0001-000000000004",
  "RoleVisibilityMatrix": {
    "Requester": [ "Management", "Finance" ],
    "Management": [ "Requester" ],
    "Finance": []
  }
}
```
* **Governance & Invariants**:
  * **Self-Department**: Every user can always view tags belonging to their own department (`tag.TagDepartmentId == viewer.DepartmentId`).
  * **Admin**: Always has full read visibility across all departments.
  * **Strict Department Mapping (No Fallback)**: Eliminates brittle string matching by mapping exact `FinanceDepartmentId` and `ManagementDepartmentId` GUIDs. If a department is not set in `AppSetting`, zero fallback is performed and cross-department permissions for that role remain disabled.
  * **Requester Role Awareness**: If a request is submitted by a Finance user, they are evaluated under the `Finance` policy (default: own department only) to prevent privilege escalation on their own tickets.
  * **Read-Only**: Cross-department visibility grants strictly read-only rights — viewers cannot remove another department's tags.

#### B. UI Disambiguation for Duplicate Tag Names
When a user has permission to view tags from multiple departments on a single ticket:
* Tags are rendered with a department prefix, badge, or distinct grouping:
  * Example: `[Marketing] #urgent` and `[Finance] #urgent`
* Search filters group available tags under department headers:
  * *My Department Tags* (clickable / selectable)
  * *Requester Department Tags* (clickable / selectable)

---

### 5. System Limits & Configuration

* **Max Tags Per Department Per Ticket**:
  * Configured via `AppSetting` (key: `TagSettings:MaxTagsPerDepartmentOnTicket`, default: `5`).
  * Enforced in the Domain aggregate: a single department cannot attach more than the configured limit to a single budget request.
* **Audit Trail Integration**:
  * Every tag addition and tag removal writes an entry into the existing `BudgetRequestModification` audit log:
    * E.g., `Added tag '#client-dinner' (Sales)` or `Removed tag '#audit-pending' (Finance)`.
