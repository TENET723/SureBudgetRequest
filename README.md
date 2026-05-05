# SureBudgetRequest — Project Handoff

**Read this first if you're continuing this project in a new Claude session.**

---

## What this project is

Internal budget-request app for **aSure** (a Myanmar company). Employees submit budget requests; requests flow through Department Head → Boss (if over limit) → Finance for approval, then Finance records payments (possibly in installments).

Replaces an existing Airtable form. Internal-network only. Lean and simple is the priority.

## Stack

- **.NET 10** (latest LTS) — note this, not .NET 9 which was the original plan
- **Blazor Server** — single project for the web layer
- **Clean Architecture** — 4 projects: Domain, Application, Infrastructure, Web
- **Supabase (PostgreSQL)** — database, accessed via Npgsql + EF Core (NOT the REST/PostgREST client)
- **Slack incoming webhook** for notifications
- **No auth in v1** — dev impersonation cookie identifies the current user
- **Custom JSON-based language service**, NOT `.resx`/`IStringLocalizer`
- **Hosting: on-premise** server at aSure office

## Project structure

```
SureBudgetRequest.slnx                      ← uses new .slnx solution format
├── SureBudgetRequest.Domain/               ← no dependencies
├── SureBudgetRequest.Application/          ← depends on Domain
├── SureBudgetRequest.Infrastructure/       ← depends on Application
└── SureBudgetRequest.Web/                  ← Blazor Server, composition root
```

**Important**: `SureBudgetRequest.Web.csproj` must reference BOTH Application AND Infrastructure (composition root pattern). At time of handoff this needs to be added — Web currently only references Application.

## Build status

| Layer | Status |
|---|---|
| Solution + 4 projects scaffolded | ✅ Done |
| Web → Infrastructure project reference | ❌ **Not added yet — fix first** |
| `Class1.cs` placeholders | ❌ **Delete from all 3 class libraries** |
| Domain layer code | ✅ Complete (in this folder, drop in) |
| Application layer | ⬜ Not started |
| Infrastructure layer | ⬜ Not started |
| Web pages | ⬜ Not started (default Blazor template still present) |

## What was decided (read the full spec)

The full requirements spec is in **`budget-request-requirements.md`** (v4). Read it.
The most important decisions:

1. **Roles**: Employee (baseline), DepartmentHead, Boss, Finance, Admin — single role per user
2. **Boss**: a single user company-wide, not one per department
3. **Limit**: per-request threshold, NOT cumulative monthly/yearly
4. **Approval**: strictly sequential (Dept Head → Boss if over limit → Finance)
5. **Auto-approval rule (R9)**: when the assigned approver of a stage equals the requester, that stage auto-approves at submission. This handles dept head and boss self-approval cleanly.
6. **Finance options**: Approve / Reject / Send back (with comment, returns to requester for fixing; resubmission restarts the chain at Dept Head)
7. **Partial payment = installments only** (Finance records each payment as it happens; sum must equal approved amount exactly; status auto-transitions Approved → PartiallyPaid → Paid)
8. **Snapshots at submission**: department, limit, dept head id, boss id are all snapshotted onto `BudgetRequest` so later changes don't re-route in-flight requests
9. **No advance withdrawal in v1** — deferred to v2; flagged in a comment in `BudgetRequest.cs`

## Domain layer conventions

- **Aggregate root: `BudgetRequest`**. All workflow rules live as methods on this entity.
- **Encapsulation**: private setters; `internal` constructors on `ApprovalAction`/`Payment`/`Attachment` so they can ONLY be created through methods on `BudgetRequest`. This is what enforces "sum of payments cannot exceed approved amount" globally.
- **Snapshots**: `DepartmentLimitAtSubmission`, `DeptHeadIdAtSubmission`, `BossIdAtSubmission` (nullable) on every request.
- **`Result<T>`** for expected failures (wrong status, etc.). Exceptions only for programmer errors (negative amount, null required field).
- **`partial class BudgetRequest`** split across 4 files: `BudgetRequest.cs` (props), `.Lifecycle.cs` (CreateDraft/Submit/Cancel/AddAttachment/UpdateDetails), `.Approvals.cs` (ApproveBy/Reject/SendBack/ResubmitAfterSendBack), `.Payments.cs` (RecordPayment).
- **EF Core compatibility**: every entity has a private parameterless constructor.

## Recommended build order from here

1. **Housekeeping**:
   - Add Web → Infrastructure project reference
   - Delete the 3 `Class1.cs` files
   - Delete default `Counter.razor` and `Weather.razor` from Web (and remove their nav links)
   - Drop in the Domain files from this zip
   - `dotnet build` should succeed

2. **Application layer**:
   - `IAppDbContext` (so Application doesn't reference EF Core directly)
   - `ICurrentUser` (provides current user's Id/Role/DepartmentId — backed by dev impersonation cookie in Web)
   - `INotificationService` (interface; implementation = Slack in Infrastructure)
   - `ILocalizer` (custom JSON-based language service interface)
   - `IFileStorage` (for attachments; LocalFileStorage or SupabaseFileStorage later)
   - `IDateTimeProvider` (for testability)
   - Use case services, one per action: `SubmitBudgetRequestService`, `ApproveRequestService`, `RejectRequestService`, `SendBackRequestService`, `ResubmitRequestService`, `CancelRequestService`, `RecordPaymentService`, plus query services for inboxes/lists
   - DTOs for input/output of each use case
   - FluentValidation validators (optional but recommended)

3. **Infrastructure layer**:
   - Packages: `Microsoft.EntityFrameworkCore`, `Npgsql.EntityFrameworkCore.PostgreSQL`, `Microsoft.EntityFrameworkCore.Design`, `EFCore.NamingConventions`
   - `AppDbContext` implementing `IAppDbContext`. Use `UseNpgsql` + `UseSnakeCaseNamingConvention()`. Store enums as `int`, timestamps as `timestamptz` with `DateTimeKind.Utc`.
   - `IEntityTypeConfiguration<T>` per entity. For `BudgetRequest` configure backing fields for the private `_approvalActions`, `_payments`, `_attachments` lists.
   - Initial migration; seed (a few departments, ~5 users with different roles, one Boss).
   - `SlackNotificationService` implementing `INotificationService` via `HttpClient` to webhook URL from config. Use Block Kit payloads. Use `NotificationOutbox` table + `BackgroundService` so Slack outages don't lose messages.
   - `JsonLocalizer` implementing `ILocalizer`. Loads `Resources/en.json` and `Resources/my.json` once at startup. Scoped to Blazor circuit so each user has their own current language. Falls back to English on missing keys.
   - `LocalFileStorage` writing to `App_Data/attachments/{requestId}/{guid}_{originalFileName}` — OR `SupabaseFileStorage` if user prefers Supabase Storage.
   - `DependencyInjection.cs` with `AddInfrastructure(IConfiguration config)` extension method.

4. **Web layer**:
   - `Program.cs` wires up `AddApplication()` and `AddInfrastructure(builder.Configuration)`
   - Cookie auth scheme for the dev impersonation. `ICurrentUser` reads the cookie.
   - Top-nav "Acting as: [user dropdown]" for switching users during testing
   - Pages: `/`, `/requests/new`, `/requests/{id}`, `/requests/mine`, `/requests/inbox` (role-aware), `/admin/users`, `/admin/departments`, `/admin/limits`
   - Language toggle in nav; persists to user's `PreferredLanguage` (when we add that field)

## Postgres-specific touches

- **Enums as `int` columns**, NOT native PG enums (easier to evolve)
- **`DateTimeKind.Utc` everywhere**; columns as `timestamptz`. Npgsql is strict.
- **`EFCore.NamingConventions` + `.UseSnakeCaseNamingConvention()`** so DB has `budget_requests`, `approval_actions`, etc., while C# stays PascalCase.
- **Disable RLS** on these tables in Supabase (we connect with service-level credentials, not anon key).
- **Connection**: use Supabase session pooler on port 5432 for EF Core.

## Configuration shape

```jsonc
// appsettings.json (Web project)
{
  "ConnectionStrings": {
    "Supabase": "Host=...;Port=5432;Database=postgres;Username=...;Password=..."
  },
  "Slack": {
    "WebhookUrl": "https://hooks.slack.com/services/..."
  },
  "Storage": {
    "AttachmentsRoot": "App_Data/attachments"
  }
}
```

## Open items (have safe defaults — see spec §8)

- F6: Dept Head on extended leave — Admin can re-assign approver
- F11: What "Paid" means precisely (current default: Finance manually marks; sum of payments must equal approved amount)
- Currency: MMK only for v1
- Departments at launch: not yet specified; seed with ~3-5 sample departments

## Files in this handoff

- `README.md` — this file
- `budget-request-requirements.md` — full spec (v4)
- `SureBudgetRequest.Domain.zip` — the Domain layer (drop into the project)

---

**Last updated**: end of planning + Domain phase. Next session should pick up at "Application layer".
