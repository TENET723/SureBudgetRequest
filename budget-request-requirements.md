# SureBudgetRequest — Project Handoff

**Read this first if you're continuing this project in a new Claude session.**

---

## What this project is

Internal budget-request app for **aSure** (a Myanmar company). Employees submit budget requests that flow through Department Head → Management (if over budget limit) → Finance for approval. Finance records payments, which may be made in installments. Advance-type requests go through a reconciliation phase after payment.

Replaces an existing Airtable workflow. Deployed on an internal server at the aSure office, also accessible at `https://surebudget.sure.com.mm`.

---

## Stack

- **.NET 9** — all four projects target `net9.0` (EF Core 9, Npgsql 9)
- **Blazor Server SSR** with `AddInteractiveServerComponents`
- **Clean Architecture** — 4 projects: Domain, Application, Infrastructure, Web
- **Supabase (PostgreSQL)** — accessed via EF Core + Npgsql (NOT the Supabase REST client)
- **EFCore.NamingConventions** — snake_case table/column names; C# stays PascalCase
- **MediatR 12** — CQRS; commands and queries in the Application layer
- **Cookie authentication** — PBKDF2-SHA256 password hashing; 8-hour sliding session
- **Slack incoming webhooks** — per-department routing; outbox pattern via `NotificationOutbox` table + `NotificationOutboxProcessor` background service
- **Supabase Storage** (default) or **LocalFileStorage** (dev fallback) for attachments
- **ClosedXML** — Excel export on the Reports page

---

## Project structure

```
SureBudgetRequest.slnx
├── SureBudgetRequest.Domain/           ← no dependencies
├── SureBudgetRequest.Application/      ← depends on Domain; MediatR, FluentValidation
├── SureBudgetRequest.Infrastructure/   ← depends on Application; EF Core, Npgsql, ClosedXML
└── SureBudgetRequest.Web/              ← Blazor Server; references Application + Infrastructure
```

Web references both Application and Infrastructure (composition root pattern). This is correct and already wired.

---

## Build status

All four layers are complete and the app is in active use.

| Layer | Status |
|---|---|
| Domain | ✅ Complete |
| Application | ✅ Complete |
| Infrastructure | ✅ Complete |
| Web (pages + layout) | ✅ Complete |
| Authentication | ✅ Full cookie auth with login page and password change flow |
| Migrations | ✅ 27 migrations applied; snapshot up to date |
| Seeder | ✅ Seeds currencies, COAs, withdraw methods, bank accounts, budget categories, users, departments |
| Deployment | ✅ Linux publish profile → `ubuntu@56.10.4.71`; systemd service `web.service` |

---

## Roles

| Role | Description |
|---|---|
| `Employee` | Submits requests. Default role. |
| `DepartmentHead` | Approves requests from their own department. One per department. |
| `Management` | Approves over-limit requests company-wide (replaces "Boss" from early spec). |
| `Finance` | Final approval stage, records payments. Split into two sub-types via `IsFinanceApprover` flag — see below. |
| `Admin` | Manages users, departments, COAs, currencies, bank accounts, withdraw methods, app settings. |
| `Accounting` | Read-only. Sees everything Finance sees but cannot take any write actions. Enforced at the command boundary — no command handler admits `Accounting` on write paths. |

### Finance sub-types (`IsFinanceApprover` flag on `User`)

| Sub-type | Can do |
|---|---|
| Finance Approver (`IsFinanceApprover = true`) | Approve, Reject, Send Back, Record Payment |
| Finance Payer (`IsFinanceApprover = false`) | Record Payment only |

The flag is cleared automatically when a user's role changes away from Finance.

---

## Approval flow

Sequential: **DeptHead → Management (if over limit) → Finance**

Whether Management is required is determined at submission time by comparing `RequestedAmountInMmkAtSubmission` against `DepartmentLimitAtSubmission`. This comparison is snapshotted and never re-evaluated; future limit changes do not re-route in-flight requests.

**Auto-approval:** if the DeptHead snapshot ID matches the requester at submission, the DeptHead stage is auto-approved immediately. Recorded in `ApprovalAction` with `Decision = AutoApproved`. Management and Finance do not currently have an equivalent guard — any Management or Finance user (other than the snapshotted DeptHead) can approve.

### Status values

```
Draft
  └─ Submit ──► PendingDeptHead ──► PendingManagement (if over limit)
                                  └─ PendingFinance
                                       ├─ Approved ──► PartiallyPaid ──► Paid
                                       │                      (Advance: PendingReconciliation ──►
                                       │                        AwaitingRefund | AwaitingReimbursement ──► Reconciled)
                                       ├─ Rejected
                                       └─ SentBack ──► (requester edits) ──► resubmit ──► PendingDeptHead
Cancelled  (requester cancels while PendingDeptHead only)
```

`Advance` requests never reach `Paid`; they go to `PendingReconciliation` after full payment. The reconciliation phase may end in `AwaitingRefund` (underspent) or `AwaitingReimbursement` (overspent) before reaching `Reconciled`.

### Worked examples

**Case A — Regular employee, under per-request limit:**
```
Submitted → PendingDeptHead (manual) → PendingFinance (manual + COA required) → Approved → [installments] → Paid
```

**Case B — Regular employee, over per-request limit:**
```
Submitted → PendingDeptHead → PendingManagement → PendingFinance (+ COA required) → Approved → Paid
```

**Case C — DeptHead submits (auto-approval):**
```
Submitted → DeptHead AUTO-APPROVED → PendingFinance (+ COA required) → Approved → Paid
```

**Case D — DeptHead submits, over limit:**
```
Submitted → DeptHead AUTO-APPROVED → PendingManagement (manual) → PendingFinance → Approved → Paid
```

**Case E — Monthly cap triggered (under per-request limit):**
```
Submitted (with MonthlyOverrunJustification) → PendingDeptHead → PendingFinance → Approved → Paid
  ↑ routing unchanged; only justification required
```

**Case F — Finance sends back, then re-approves:**
```
... → PendingFinance → SentBack → [requester edits] → resubmit → PendingDeptHead → PendingFinance
  [Finance re-approves; CoaId pre-filled from prior approval; Finance can change it] → Approved
```

**Case G — Advance request:**
```
Submitted → PendingDeptHead → PendingFinance (sets ReconciliationDeadline + COA) → Approved
  → [installments] → PendingReconciliation → [requester submits usage]
  → AwaitingRefund (underspent) or AwaitingReimbursement (overspent) → Reconciled
```

---

## Business rules

### Currency

- **R1** — A request is denominated in a single currency chosen at draft time from the active `Currency` master list.
- **R1a** — `RequestedAmount`, `ApprovedAmount`, and `Payment.Amount` are all in the request's currency. Only the limit comparison converts to MMK.
- **R13** — MMK is locked: its rate cannot be changed and it cannot be deactivated.
- **R12** — All currency rate changes by admin write a `CurrencyRateChange` audit row in the same transaction.

### Exchange rate

- **R7** — The per-request limit comparison uses the department's current per-request limit AND the effective exchange rate at submission time. The effective rate is either a manual override supplied by the requester (R28) or the current system rate at the moment of submission. Both are snapshotted onto the request.
- **R7a** — Snapshot fields: `CurrencyCode`, `ExchangeRateAtSubmission`, `RequestedAmountInMmkAtSubmission`. Immutable once set.
- **R14** — A `SentBack` request that is resubmitted picks up the then-current exchange rate, dept head, and monthly limit.
- **R28** — For non-Advance requests, the requester may manually override the system exchange rate during Draft or SentBack. If set, it is used for limit comparisons and MMK snapshots at submission.
- **R29** — Advance requests always use the system exchange rate. Manual overrides are forbidden for this type.
- **R30** — Manual exchange rates must be greater than zero.

### Per-request budget limit

- **R6** — `Department.BudgetLimit` (in MMK) is a per-single-request threshold. A request whose MMK-equivalent exceeds it routes through the Management stage.
- **R7** — Limit comparison uses `DepartmentLimitAtSubmission`. Future limit changes do not re-route in-flight requests.

### Monthly budget cap

- **R15** — Each department has an optional `MonthlyLimit` (in MMK, nullable). When set, it caps cumulative approved spend for the current calendar month. When null, no monthly enforcement is performed.
- **R16** — Monthly spend = sum of `RequestedAmountInMmkAtSubmission` for requests where `DepartmentIdAtSubmission` matches, `SubmittedAt` is within the calendar month (UTC), and `Status ∈ {Approved, PartiallyPaid, Paid}`.
- **R17** — At submission, if the new request would push the dept over its monthly limit, `MonthlyOverrunJustification` must be non-empty. `Submit()` fails otherwise.
- **R18** — Monthly overrun does not alter routing — Management stage is only triggered by the per-request limit.
- **R19** — Snapshot `MonthlyLimitAtSubmission` and `MonthlySpendBeforeAtSubmission` on the request at submission. Both nullable.
- **R20** — On `ResubmitAfterSendBack`, the monthly check re-runs against the then-current dept monthly limit and spend.

### Chart of Accounts (COA)

- **R21** — Each `BudgetRequest` has an optional `CoaId` (nullable FK to `Coa`). Null until Finance approves; **required at Finance approval**.
- **R22** — `ApproveBy` at the Finance stage must receive a `CoaId` referencing an active `Coa`. Domain enforces non-null; Application layer verifies existence and active status.
- **R23** — The COA list is global (not per-department). `Coa` fields: `Code` (unique, max 32), `Name` (max 200), `Description?` (max 1000), `IsActive`.
- **R24** — Finance role manages the COA list. Admin also has access.
- **R25** — On send-back / re-approval, `CoaId` is preserved as a pre-fill hint for the next Finance approver. The next Finance approval overwrites it. No per-action audit of COA changes — current value wins.
- **R26** — Pre-existing approved requests (before the COA feature shipped) have `CoaId = null` indefinitely. No backfill.
- **R27** — A `Coa` cannot be deleted while any `BudgetRequest` references it (`OnDelete: Restrict`). Deactivate instead. Deactivated accounts don't appear in the approval picker but historical FKs remain intact.

### Approval rules

- **R8** — Approval is strictly sequential: DeptHead → Management (if over limit) → Finance.
- **R9** — Auto-approval: the DeptHead stage is auto-approved when `DeptHeadIdAtSubmission == RequesterId`. Recorded with `Decision = AutoApproved`. Management and Finance stages never auto-approve.
- **R10** — Every status transition writes an `ApprovalAction` row.

### Payments

- Payments are recorded as installments. Sum of all `Payment.Amount` values must equal `ApprovedAmount` (in request currency).
- Status transitions automatically: `Approved` → `PartiallyPaid` (first payment, not yet full) → `Paid` (sum equals approved amount).
- Advance requests: full payment triggers `PendingReconciliation` instead of `Paid`.

### Cancellation / rejection

- **R** — Requester can cancel only while in `PendingDeptHead`. After any approval, cancellation is blocked.
- **R** — Any approver can reject at their stage. `Rejected` is terminal. To retry, create a new request.

### Notifications

- **R11** — Slack notifications fire on every status change. Webhook URLs are stored per-department. No global webhook URL in config.

---

## Request types (`BudgetRequestType`)

| Value | Description |
|---|---|
| `Standard` | Normal purchase request. Participates in monthly spend cap check. |
| `Urgent` | Same routing as Standard; flagged for priority. Participates in monthly cap. |
| `ProjectProposal` | Does not participate in monthly cap. |
| `Advance` | Requester draws funds before knowing exact spend. Finance sets a reconciliation deadline at approval. Enters reconciliation phase after full payment. |

**Reference number format:** `BR-{TypeCode}-{yyyyMMdd}-{4 random digits}` (e.g. `BR-U-20260513-4521`). Generated at submission; null while Draft. TypeCode: `U` = Urgent, `S` = Standard, `P` = ProjectProposal, `A` = Advance.

---

## Domain layer conventions

- **`BudgetRequest` aggregate** — workflow rules live as methods on the entity. Partial class split across 5 files:
  - `BudgetRequest.cs` — properties and identity
  - `BudgetRequest.Lifecycle.cs` — `CreateDraft`, `Submit`, `Cancel`, `UpdateDetails`, `AddAttachment`
  - `BudgetRequest.Approvals.cs` — `ApproveBy`, `Reject`, `SendBack`, `ResubmitAfterSendBack`
  - `BudgetRequest.Payments.cs` — `RecordPayment`
  - `BudgetRequest.Reconciliation.cs` — `SubmitReconciliation`, `RecordRefund`, `RecordReimbursement`, advance usage tracking
- **Encapsulation** — private setters; `internal` constructors on child entities (`ApprovalAction`, `Payment`, `Attachment`, `AdvanceUsage`) so they can only be created through methods on `BudgetRequest`.
- **`Result<T>`** — for expected failures (wrong status, validation). Exceptions only for programmer errors (null required fields, negative amounts).
- **EF Core compatibility** — every entity has a private parameterless constructor.

### Submission snapshots

`BudgetRequest` captures the following at `Submit()` time so audit and routing are stable regardless of later admin changes:

- `DepartmentIdAtSubmission`, `DepartmentLimitAtSubmission`
- `DeptHeadIdAtSubmission`, `DeptHeadNameAtSubmission`
- `RequesterNameAtSubmission`
- `ExchangeRateAtSubmission`, `RequestedAmountInMmkAtSubmission`
- `MonthlyLimitAtSubmission`, `MonthlySpendBeforeAtSubmission`
- `MonthlyWindowStartAtSubmission`, `MonthlyWindowEndAtSubmission`

---

## Key entities

| Entity | Notes |
|---|---|
| `User` | Role, `IsFinanceApprover`, `DepartmentId`, `PasswordHash`, `MustChangePassword`, `SlackUserId` |
| `Department` | `HeadUserId`, `BudgetLimit` (per-request threshold in MMK), `MonthlyLimit` (cumulative cap in MMK), `SlackWebhookUrl` |
| `BudgetRequest` | Aggregate root; see above |
| `ApprovalAction` | Audit row per decision (Approved / Rejected / AutoApproved / SentBack) |
| `Payment` | Installment row; sum of payments must equal approved amount |
| `Attachment` | Linked to `BudgetRequest` or `WithdrawMethod`; stored in Supabase Storage |
| `AdvanceUsage` | Requester-reported spend line items during reconciliation |
| `BudgetRequestModification` | Log of field changes (amount, reason, etc.) made while Draft/SentBack |
| `Currency` | Code, name, exchange rate to MMK |
| `CurrencyRateChange` | Audit trail of rate updates |
| `Coa` | Chart of accounts; assigned by Finance at approval |
| `WithdrawMethod` | Payment method options (can have an attachment) |
| `BankAccount` | Company bank accounts used for payments |
| `BudgetCategory` | Category tags for requests |
| `DepartmentMonthlyBudget` | Financial-year budget plan per department per month |
| `AppSetting` | Key-value store for runtime configuration (e.g. reconciliation deadline defaults) |
| `NotificationOutboxEntry` | Infrastructure only; transactional outbox for Slack messages |

---

## Multi-currency

Requests can be denominated in any active `Currency`. The `ExchangeRateAtSubmission` (to MMK) is snapshotted at submit time. `RequestedAmountInMmkAtSubmission` is cached for limit comparisons and reporting. An optional `ManualExchangeRate` override is supported for non-Advance requests.

---

## Monthly budget cap

`Standard` and `Urgent` requests participate in a monthly spend cap. At submission:

1. The app calculates the department's already-approved spend in MMK for the current calendar month window.
2. If the new request would push total spend over `Department.MonthlyLimit`, the requester must provide a `MonthlyOverrunJustification` — `Submit()` fails without it.
3. Routing is not affected — only the per-request `BudgetLimit` triggers the Management stage.

The `PeriodOverrunBadge` and `MonthlyLimitBadge` shared components surface this in the UI.

---

## Notifications (Slack)

Notifications are written to the `NotificationOutbox` table within the same EF Core transaction as the domain change (via `SlackNotificationService`). `NotificationOutboxProcessor` (a `BackgroundService`) polls every 10 seconds (configurable) and POSTs pending entries to Slack.

Webhook URLs are stored per-department on `Department.SlackWebhookUrl`, managed in Admin → Departments. There is no global webhook URL in config.

`MaxRetries` (default 5) is configured under `Slack` in `appsettings.json`. Network/config errors consume retries the same as Slack rejections — monitor for exhausted entries on misconfigured startups.

**Ordering rule:** `SaveChangesAsync` must be called before `DispatchAsync` — the outbox entry must be persisted before the background processor can pick it up.

---

## File storage

Configured via `Storage:Provider` in `appsettings.json`:

- `"Supabase"` (default) — `SupabaseFileStorage`; requires `Supabase:Url`, `Supabase:ServiceRoleKey`, `Supabase:AttachmentsBucket`
- `"Local"` — `LocalFileStorage`; writes to `Storage:AttachmentsRoot` (default `App_Data/attachments/{requestId}/{guid}_{originalFileName}`)

Attachment file-size limits are defined in `AttachmentConstraints.cs`. The SignalR max message size in `Program.cs` is bumped to match.

---

## Authentication

Full cookie-based auth. No dev impersonation — users log in with a real username/password.

- Cookie name: `.SureBudget.Auth`
- Session: 8-hour sliding expiration
- Passwords hashed with PBKDF2-SHA256 via `IPasswordHasher`
- `MustChangePassword` flag: set on creation and admin resets; clears after a successful self-service password change. Users are redirected to the change-password page until cleared.
- `ICurrentUser` is resolved from cookie claims via `AuthenticationStateProvider` on the Blazor circuit. Inside `ScopedMediator` operation scopes, `CurrentUserSnapshot` is populated from the circuit user before each handler runs.

---

## ScopedMediator

Blazor Server's single DI scope per circuit means one `AppDbContext` per browser session. Fast page switching can trigger concurrent operations on that single context. `ScopedMediator` wraps every `Send`/`Publish` in its own DI scope so each command/query gets its own `DbContext`. It replaces MediatR's default `IMediator` registration and must be registered after `AddApplication()`/`AddInfrastructure()`.

---

## Configuration shape

```jsonc
// appsettings.json
{
  "ConnectionStrings": {
    "Supabase": "Host=...;Port=5432;Database=postgres;Username=...;Password=...;Trust Server Certificate=true"
  },
  "Slack": {
    // Webhook URLs are per-department (stored in DB). No global URL here.
    "MaxRetries": 5,
    "PollingIntervalSeconds": 10
  },
  "Storage": {
    "Provider": "Supabase",          // "Supabase" | "Local"
    "AttachmentsRoot": "App_Data/attachments"  // only used when Provider = "Local"
  },
  "Supabase": {
    "Url": "https://<project>.supabase.co",
    "ServiceRoleKey": "<service-role-jwt>",
    "AttachmentsBucket": "budget-attachments"
  }
}
```

**Connection port:** use Supabase session pooler on **port 5432** for EF Core. Port 6543 (transaction pooler) is unsuitable for EF migrations and breaks prepared statements.

---

## Web pages

| Route | Description |
|---|---|
| `/login` | Public login page |
| `/account/change-password` | Forced on first login; also self-service |
| `/` | Dashboard |
| `/requests/new` | New request form |
| `/requests/{id}` | Request detail / action page |
| `/requests/{id}/edit` | Edit draft or sent-back request |
| `/requests/mine` | My requests list |
| `/requests/inbox` | Approval inbox (role-aware) |
| `/requests/outstanding-payments` | Finance payment queue |
| `/reports/budget-requests` | Filterable report with Excel export |
| `/finance/budget-plan` | Department monthly budget plan |
| `/admin/users` | User management |
| `/admin/departments` | Department management |
| `/admin/department-heads` | Department head assignment |
| `/admin/currencies` | Currency and exchange rate management |
| `/coas` | Chart of accounts |
| `/bank-accounts` | Bank account management |
| `/withdraw-methods` | Withdraw method management |
| `/admin/budget-categories` | Budget category management |
| `/app-settings` | Runtime app settings |
| `/account/profile` | User profile (placeholder) |

---

## Deployment

Publish profile: `SureBudget-Linux-Prod.pubxml`

- Runtime: `linux-x64`, framework-dependent
- Publishes to `bin/Release/net9.0/publish/`, then SCPs to `ubuntu@56.10.4.71:/var/www/web`
- Restarts `web.service` via SSH after deploy
- Live at `https://surebudget.sure.com.mm`
- SSH key: `C:\Users\WIN\source\repos\publishKey\LightsailDefaultKey-ap-southeast-1a.pem`

**EF migrations** must be run from the Web project using port 5432 (session pooler):
```
dotnet ef database update --project SureBudgetRequest.Infrastructure --startup-project SureBudgetRequest.Web
```

---

## Known issues / on the horizon

- **Self-approval gap at Management and Finance stages** — auto-approval is only enforced at DeptHead (requester == `DeptHeadIdAtSubmission` is caught at `Submit()`). Management and Finance lack an `approver != RequesterId` guard. Flagged as highest priority.
- **Profile page** — `Account/Profile.razor` exists but is a placeholder; not implemented.
- **Slack reference numbers** — Slack notification payloads do not yet include the `Reference` field added during the reference-number rollout.
- **`Counter.razor` and `Weather.razor`** — default Blazor template pages still present in the Web project; they have no nav links but haven't been deleted.

---

## Key learnings / gotchas

- **Spec drift** — the `budget-request-requirements.md` file reflects early planning and diverges significantly from the built system. Always read the codebase before making recommendations.
- **Snapshot principle** — filtering and display operate on values snapshotted at submission time, not live data. This is the most common source of confusion when something "doesn't update."
- **EF Core migration hygiene** — always use `dotnet ef migrations add` rather than hand-writing migrations. Missing `.Designer.cs` files cause silent schema drift.
- **Antiforgery in Blazor SSR** — `EditForm` with `method="post"` auto-injects the antiforgery token. Do NOT add `<AntiforgeryToken />` inside an `EditForm`; it causes duplicate fields and validation failure. Manual token is only required in plain HTML `<form>` elements.
- **Notification outbox ordering** — `SaveChangesAsync` before `DispatchAsync`. Reversing this drops outbox entries on `DbContext` disposal.
- **Retry exhaustion** — `MaxRetries` is consumed equally by network errors and Slack rejections. A misconfigured startup will permanently exhaust retries for in-flight notifications.
- **RLS** — disable Row Level Security on all app tables in Supabase. The app connects with service-role credentials, not the anon key.