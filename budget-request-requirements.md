# Budget Request — Requirements Spec (v4)

**Project:** SureBudgetRequest (aSure internal app)
**Stack:** .NET 10 · Blazor Server · Clean Architecture · Supabase (PostgreSQL) · Slack notifications
**Status:** Approved for build (minor open items in §8)

---

## 1. Overview

Internal app for aSure employees to request budget for purchases or expenses. Requests flow through a Department Head, optionally a Boss (for over-limit amounts), then Finance.

## 2. Roles

| Role | Description |
|---|---|
| **Employee** | Submits requests. Default role. |
| **Department Head** | Approves requests from their own department. One per department. |
| **Boss** | A single user company-wide. Approves over-limit requests. |
| **Finance** | Final stage — verifies, picks Chart of Account, processes payment. |
| **Admin** | Manages users, departments, budget limits. Has access to all admin pages. |

## 3. Core Entities

- **User** — belongs to a Department, has a Role
- **Department** — has one Department Head, a **per-request budget limit**, and an **optional monthly limit** (both in MMK)
- **BudgetRequest** — the request itself, with status, amount, requester, **currency**, optional **CoaId**
- **ApprovalAction** — audit row for every approve/reject/auto-approve decision
- **Attachment** — supporting documents
- **Currency** — master record (Code, Name, RateToMmk, IsActive). MMK exists as a row with rate = 1.
- **CurrencyRateChange** — audit row written every time an admin changes a currency's rate
- **Coa** (Chart of Account) — master record (Code, Name, Description?, IsActive). Global pool — every department picks from the same list.

## 4. Approval Flow

The flow is **strictly sequential**: Department Head → Boss (if applicable) → Finance.

Whether the Boss stage applies is determined by **the request amount (converted to MMK) vs. the department's *per-request* budget limit at submission time**:

- amount-in-MMK **≤ per-request limit** → skip Boss stage
- amount-in-MMK **> per-request limit** → include Boss stage

When the request's currency is not MMK, the system converts the requested amount to MMK using the **exchange rate at submission time** (snapshotted onto the request — see R7 and R7a). Department limits are always denominated in MMK.

**The monthly limit (R15) is independent of routing.** It does *not* add a Boss stage. Its only effect on the flow is to require a justification field on the request when triggered.

**Finance approval requires a Chart of Account selection (R21).** Dept Head and Management approvals do not.

### 4.1 Auto-approval rule (handles self-approval cleanly)

> **If the assigned approver of a stage is the same person as the requester, that stage is automatically approved by the system.**

This single rule handles every self-approval case at the Dept Head stage. Management and Finance stages do not auto-approve (peer review even for Management members).

The auto-approval is recorded in `ApprovalAction` with `Decision = AutoApproved` and `ApproverId = RequesterId` for full audit visibility.

### 4.2 Worked examples

**Case A — Regular employee, under per-request limit, under monthly:**
```
Submitted → PendingDeptHead (manual) → PendingFinance (manual + COA pick) → Approved → Paid
```

**Case B — Regular employee, over per-request limit:**
```
Submitted → PendingDeptHead → PendingManagement → PendingFinance (+ COA pick) → Approved → Paid
```

**Case F — Regular employee, under per-request limit, but pushes department over monthly:**
```
Submitted (with MonthlyOverrunJustification) →
  PendingDeptHead → PendingFinance (+ COA pick) → Approved → Paid
```

**Case G — Finance approves, then sends back, then re-approves:**
```
Submitted → PendingDeptHead → PendingFinance →
  [Finance sends back with comment, optionally with COA pre-selected] → SentBack →
  [Requester edits and resubmits] → PendingDeptHead → PendingFinance →
  [Finance approves; modal pre-fills the prior CoaId; Finance can change it] → Approved → Paid
```

### 4.3 Rejection

Any approver can reject at their stage:

```
Pending<Stage> → Rejected
```

`Rejected` is terminal. To resubmit, the employee creates a new request.

## 5. Status Values

- `Draft` — saved but not submitted
- `PendingDeptHead`
- `PendingManagement` — only when amount-in-MMK > department per-request limit
- `PendingFinance`
- `SentBack` — Finance returned it to the requester for fixes
- `Approved` — finance approved; awaiting payment dispatch
- `PartiallyPaid` — some payments recorded, not yet full
- `Paid` — finance has marked it paid
- `Rejected` — terminal
- `Cancelled` — withdrawn by requester before any approval

## 6. Business Rules

- **R1** — A request is denominated in a **single currency** chosen at draft time from the active Currency master list.
- **R1a** — The request's `RequestedAmount`, `ApprovedAmount`, and `Payment.Amount` are all in the request's currency. Only the limit comparison converts to MMK.
- **R2** — A request requires: amount, currency, reason, requester, request date.
- **R3** — Each department has exactly **one** Department Head.
- **R4** — Each user belongs to exactly **one** department.
- **R6** — The department **per-request limit** (in MMK) is a per-single-request threshold for routing. A request whose MMK-equivalent exceeds it triggers Management approval.
- **R7** — The per-request limit comparison uses the department's **current** per-request limit AND the **current exchange rate** at submission time. Both are snapshotted onto the request.
- **R7a** — Exchange rate snapshot fields on `BudgetRequest`: `CurrencyCode`, `ExchangeRateAtSubmission`, `RequestedAmountInMmkAtSubmission`. Immutable once set.
- **R8** — Approval is **strictly sequential**: Dept Head → Management (if applicable) → Finance.
- **R9** — **Auto-approval rule**: any Dept Head stage where the assigned approver is the requester is auto-approved by the system at submission time.
- **R10** — All status transitions write an `ApprovalAction` row.
- **R11** — Slack notifications fire on every status change.
- **R12** — All **currency rate changes** by admin write a `CurrencyRateChange` audit row in the same transaction.
- **R13** — MMK is locked: its rate cannot be changed and it cannot be deactivated.
- **R14** — A request in `SentBack` status that is resubmitted picks up the **then-current** exchange rate, dept head, and monthly limit.

### Monthly limit rules

- **R15** — Each department has an **optional `MonthlyLimit`** (in MMK, nullable). When set, it caps the department's cumulative approved-by-Finance spending for the current calendar month. When null, no monthly enforcement is performed.
- **R16** — **Monthly spend** for a department in a given calendar month is the sum of `RequestedAmountInMmkAtSubmission` for all requests where `DepartmentIdAtSubmission` matches, `SubmittedAt` falls within that calendar month (UTC, half-open), and `Status ∈ {Approved, PartiallyPaid, Paid}`.
- **R17** — At submission, if a monthly limit is set and the new request would push the dept over, the requester must have supplied a non-empty `MonthlyOverrunJustification`. Submission fails otherwise.
- **R18** — Monthly overrun does **not** alter routing.
- **R19** — At submission, snapshot `MonthlyLimitAtSubmission` and `MonthlySpendBeforeAtSubmission` on the request. Both nullable.
- **R20** — On `ResubmitAfterSendBack`, the monthly check re-runs against the then-current dept monthly limit and spend.

### Chart of Account rules

- **R21** — Each `BudgetRequest` has an optional `CoaId` (nullable FK to `Coa`). Null until Finance approves; required at Finance approval; preserved through send-back / re-approval cycles.
- **R22** — At the Finance stage, `ApproveBy` must receive a `CoaId` referencing an **active** `Coa`. The domain enforces non-null; the Application layer additionally verifies existence and active status.
- **R23** — The Chart of Account master list is **global** (not per-department). `Coa` fields: `Code` (unique, max 32), `Name` (max 200), `Description?` (max 1000), `IsActive`.
- **R24** — **Finance** role manages the COA master list. **Admin** also has access as a safety net.
- **R25** — On send-back / re-approval, `CoaId` is **preserved** as a pre-fill hint for the next Finance approver. The next Finance approval overwrites it. No per-action audit of COA changes — current value wins.
- **R26** — Pre-existing approved requests (created before this feature shipped) have `CoaId = null` indefinitely. No backfill is performed.
- **R27** — A `Coa` cannot be deleted while any `BudgetRequest` references it (FK `OnDelete: Restrict`). Admins **deactivate** unused accounts instead of deleting them. Deactivated accounts don't appear in the approval picker, but historical references remain intact.

---

## 7. Resolved Decisions

| ID | Decision |
|---|---|
| F2 — Self-approval | Auto-approve any Dept Head stage where approver = requester (R9). Management/Finance never auto-approve. |
| F3 — Limit period | Per-request limit AND optional monthly limit, both coexist (R6, R15). |
| F4 — Approval order | Sequential: Dept Head → Management → Finance. |
| F13 — Multi-currency | Supported as of v2. |
| F16 — Monthly limit | Added in v3. Optional per-department cap; triggers justification. |
| F17 — Month definition | Determined by `SubmittedAt` (UTC). |
| F20 — Chart of Account | Added in v4. Required at Finance approval. Global pool. Finance role manages, Admin has access too. |
| F21 — COA history | Not tracked per-action in v1 — `BudgetRequest.CoaId` is overwritten on re-approval. |
| F22 — COA backfill | Existing approved requests stay null. No backfill. |

---

## 8. Still Open (decide before launch, not blocking build)

| ID | Question | Recommended default |
|---|---|---|
| **F6** | What if Dept Head is on leave for weeks? | Admin can re-assign the approver on a stuck request |
| **F7** | Can requester edit a submitted request? | **No.** Cancel and resubmit. |
| **F8** | Can requester cancel? At which stages? | Only while in `PendingDeptHead`. |
| **F18** | Should month boundaries use UTC or Myanmar local time (UTC+6:30)? | UTC for v1. |
| **F23** | Should COA selection be tracked per-ApprovalAction (full audit history)? | Not in v1. Add to ApprovalAction in v2 if it matters. |
| **F24** | Should we expose "approved spend by COA" reports? | Out of scope for v1. The data is there when needed (FK + index on `coa_id`). |

---

## 9. Notification Triggers (Slack)

Unchanged from v3.

---

## 10. Key Implementation Notes

10.1 — `BudgetRequest` aggregate owns its workflow.
10.2 — Stage routing logic lives in the Domain.
10.3 — Auto-approval is computed at `Submit()`-time.
10.4 — Submission snapshots: `DepartmentLimit`, `ExchangeRate`, `RequestedAmountInMmk`, `MonthlyLimit?`, `MonthlySpendBefore?`.
10.5 — Currency is a separate aggregate; referenced by `CurrencyCode` string FK.
10.6 — Monthly-spend aggregate query lives in the repository.
10.7 — **`Coa` is a separate aggregate**; referenced by `BudgetRequest.CoaId` (Guid FK). The Domain `ApproveBy` method takes `coaId` as a parameter; the Application layer validates active-status before calling.
10.8 — `Coa` lookups for display (Detail page) are done in the `GetBudgetRequestQuery` handler — `BudgetRequestDto` carries `CoaCode` and `CoaName` as display fields, resolved server-side from the FK.

---

## 11. Currency Management

- Admin-only UI at `/admin/currencies`. Unchanged from v3.

## 12. Chart of Account Management

- Finance + Admin UI at `/coas`. CRUD with create/edit modal, active/inactive toggle, deactivate confirm. Same shape as the Currencies admin page.
- Codes are unique (case-sensitive) and trimmed on save.
- Deactivation does NOT cascade to historical requests — the FK is preserved, but the account won't appear in the approval picker for new approvals.
- Deletion is blocked by the FK (`OnDelete: Restrict`). Always deactivate.
