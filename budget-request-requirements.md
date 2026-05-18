# Budget Request — Requirements Spec (v3)

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
| **Finance** | Final stage — verifies and processes payment. |
| **Admin** | Manages users, departments, budget limits, and **currencies / exchange rates**. |

## 3. Core Entities

- **User** — belongs to a Department, has a Role
- **Department** — has one Department Head, a **per-request budget limit**, and an **optional monthly limit** (both in MMK)
- **BudgetRequest** — the request itself, with status, amount, requester, **currency**
- **ApprovalAction** — audit row for every approve/reject/auto-approve decision
- **Attachment** — supporting documents
- **Currency** — master record (Code, Name, RateToMmk, IsActive). MMK exists as a row with rate = 1.
- **CurrencyRateChange** — audit row written every time an admin changes a currency's rate

## 4. Approval Flow

The flow is **strictly sequential**: Department Head → Boss (if applicable) → Finance.

Whether the Boss stage applies is determined by **the request amount (converted to MMK) vs. the department's *per-request* budget limit at submission time**:

- amount-in-MMK **≤ per-request limit** → skip Boss stage
- amount-in-MMK **> per-request limit** → include Boss stage

When the request's currency is not MMK, the system converts the requested amount to MMK using the **exchange rate at submission time** (snapshotted onto the request — see R7 and R7a). Department limits are always denominated in MMK.

**The monthly limit (R15) is independent of routing.** It does *not* add a Boss stage. Its only effect on the flow is to require a justification field on the request when triggered.

### 4.1 Auto-approval rule (handles self-approval cleanly)

> **If the assigned approver of a stage is the same person as the requester, that stage is automatically approved by the system.**

This single rule handles every self-approval case:

- A Department Head submits → their own dept head stage auto-approves
- The Boss submits → the boss stage auto-approves
- A person who is both Dept Head and Boss → both stages auto-approve (edge case, but supported)

The auto-approval is recorded in `ApprovalAction` with `Decision = AutoApproved` and `ApproverId = RequesterId` for full audit visibility.

### 4.2 Worked examples

**Case A — Regular employee, under per-request limit, under monthly:**
```
Submitted → PendingDeptHead (manual) → PendingFinance (manual) → Approved → Paid
```

**Case B — Regular employee, over per-request limit:**
```
Submitted → PendingDeptHead (manual) → PendingBoss (manual) → PendingFinance (manual) → Approved → Paid
```

**Case C — Dept Head submits, under per-request limit:**
```
Submitted → DeptHead AUTO-APPROVED → PendingFinance (manual) → Approved → Paid
```

**Case D — Dept Head submits, over per-request limit:**
```
Submitted → DeptHead AUTO-APPROVED → PendingBoss (manual) → PendingFinance (manual) → Approved → Paid
```

**Case E — Boss submits, over their own dept's per-request limit:**
```
Submitted → PendingDeptHead (manual) → Boss AUTO-APPROVED → PendingFinance (manual) → Approved → Paid
```

**Case F — Regular employee, under per-request limit, but pushes department over monthly:**
```
Submitted (with MonthlyOverrunJustification supplied) →
  PendingDeptHead (manual) → PendingFinance (manual) → Approved → Paid
```
Note: the flow is unchanged from Case A. The only difference is that the requester
*must* supply a `MonthlyOverrunJustification` when filling out the request, or
submission is rejected with a validation error. The justification appears alongside
`Reasons` on the request detail page for all approvers to see.

### 4.3 Rejection

Any approver can reject at their stage:

```
Pending<Stage> → Rejected
```

`Rejected` is terminal. To resubmit, the employee creates a new request (optionally linked to the rejected one for traceability).

## 5. Status Values

- `Draft` — saved but not submitted
- `PendingDeptHead`
- `PendingBoss` — only when amount-in-MMK > department per-request limit
- `PendingFinance`
- `Approved` — finance approved; awaiting payment dispatch
- `Paid` — finance has marked it paid
- `Rejected` — terminal
- `Cancelled` — withdrawn by requester before any approval

(`Submitted` is transient — submission moves directly into `PendingDeptHead` or auto-approves through it.)

## 6. Business Rules

- **R1** — A request is denominated in a **single currency** chosen at draft time from the active Currency master list. MMK must always be present (rate = 1) and is the system's base currency.
- **R1a** — The request's `RequestedAmount`, `ApprovedAmount`, and `Payment.Amount` are all in the request's currency. Only the limit comparison converts to MMK.
- **R2** — A request requires: amount, currency, reason, requester, request date
- **R3** — Each department has exactly **one** Department Head
- **R4** — Each user belongs to exactly **one** department
- **R5** — There is exactly **one** Boss role-holder in the system at a time
- **R6** — The department **per-request limit** (in MMK) is a per-single-request threshold for routing. A request whose MMK-equivalent exceeds it triggers Boss approval. It is NOT cumulative.
- **R7** — The per-request limit comparison uses the department's **current** per-request limit AND the **current exchange rate** at submission time. Both are snapshotted onto the request. Future limit or rate changes do not retroactively re-route in-flight requests.
- **R7a** — Exchange rate snapshot fields on `BudgetRequest`: `CurrencyCode` (also stored from draft creation), `ExchangeRateAtSubmission`, `RequestedAmountInMmkAtSubmission`. These are immutable once set.
- **R8** — Approval is **strictly sequential**: Dept Head → Boss (if applicable) → Finance. No parallel approvals.
- **R9** — **Auto-approval rule**: any stage where the assigned approver is the requester is auto-approved by the system at submission time.
- **R10** — All status transitions (manual approvals, rejections, auto-approvals) write an `ApprovalAction` row.
- **R11** — Slack notifications fire on every status change.
- **R12** — All **currency rate changes** by admin write a `CurrencyRateChange` audit row in the same transaction (old rate, new rate, changed-by, timestamp).
- **R13** — MMK is locked: its rate cannot be changed and it cannot be deactivated.
- **R14** — A request in `SentBack` status that is resubmitted picks up the **then-current** exchange rate (rate is re-snapshotted, not preserved from the original submission), matching how the dept head / limit are re-evaluated.

### Monthly limit rules

- **R15** — Each department has an **optional `MonthlyLimit`** (in MMK, nullable). When set, it caps the department's *cumulative* approved-by-Finance spending for the current calendar month. When `null`, no monthly enforcement is performed for that department (backwards-compatible with departments that pre-date the feature).
- **R15a** — Setting `MonthlyLimit` is the admin's call to enable enforcement. Setting it to a non-null value (including 0) enables the check. Clearing it back to null disables.
- **R16** — **Monthly spend** for a department in a given calendar month is defined as the sum of `RequestedAmountInMmkAtSubmission` for all requests:
  - `DepartmentIdAtSubmission` equals that department
  - `SubmittedAt` falls within that calendar month (UTC, half-open `[start, end)`)
  - `Status` is one of `Approved`, `PartiallyPaid`, `Paid`
  - i.e. `Draft`, `Pending*`, `SentBack`, `Rejected`, `Cancelled` requests are excluded.
- **R17** — **Monthly overrun check at submission**: if `MonthlyLimit` is set AND `(MonthlySpend + RequestedAmountInMmk) > MonthlyLimit`, the requester must have provided a non-empty `MonthlyOverrunJustification` on the request. Submission fails otherwise.
- **R18** — Monthly overrun does **not** alter routing. It does not add the Boss stage. The flow is identical to a non-overrun request; only the justification field is required.
- **R19** — At submission, the request snapshots `MonthlyLimitAtSubmission` (the dept's monthly limit at that moment) and `MonthlySpendBeforeAtSubmission` (the dept's monthly spend just before this request was counted). Both are nullable — null when monthly enforcement was off for the dept at submission. The justification text, once entered, is preserved on the request for audit even if it ended up not being strictly required (low-cost; aids audit).
- **R20** — On `ResubmitAfterSendBack`, the monthly check re-runs against the *then-current* dept monthly limit and spend (parallel to R14). If conditions have changed during the fix, the justification requirement may now apply or no longer apply.

### Known limitation (acceptable for v1)

The monthly check happens at submission time using only Finance-approved requests in the totals. Two concurrent submissions can therefore each pass the at-submission check, even if together they would exceed the monthly limit. Finance sees the running total and is the de facto backstop. If this becomes a real-world issue, R16's status list can be widened to include `Pending*` for the at-submission check only.

---

## 7. Resolved Decisions

These were open in v1 and are now decided:

| ID | Decision |
|---|---|
| F1 — Who is Boss | Single user company-wide with Boss role |
| F2 — Self-approval | Auto-approve any stage where approver = requester (R9) |
| F3 — Limit period | Per-request limit AND optional monthly limit, both coexist (R6, R15) |
| F4 — Approval order | Sequential: Dept Head → Boss → Finance |
| F5 — Race condition | Known limitation, accepted for v1 — see "Known limitation" above |
| F13 — Multi-currency | Supported as of v2. See R1, R1a, R7, R7a, R12, R13, R14. |
| F16 — Monthly limit | Added in v3. Optional per-department cap; triggers justification, not extra approval. See R15–R20. |
| F17 — Month definition | A request's "month" is determined by `SubmittedAt` (UTC). |

---

## 8. Still Open (decide before launch, not blocking build)

| ID | Question | Recommended default |
|---|---|---|
| **F6** | What if Dept Head is on leave for weeks? | Admin can re-assign the approver on a stuck request |
| **F7** | Can requester edit a submitted request? | **No.** Cancel and resubmit. |
| **F8** | Can requester cancel? At which stages? | Only while in `PendingDeptHead`. After any approval, cannot cancel. |
| **F9** | Can Finance reject? | Yes — three options: Approve, Mark as Paid, or Send back to Requester (with comment, returns to `Draft`). |
| **F10** | Partial approval (from old Airtable form)? | **Drop for v1.** Approve in full or reject. |
| **F11** | What does `Paid` mean? | Finance manually sets it after payment is dispatched, with optional payment reference field. |
| **F12** | What if employee changes department mid-flow? | Lock the dept head at submission time. Department change does not re-route in-flight requests. |
| **F14** | Should approvers see the original-currency amount or the MMK-equivalent? | Show both side-by-side; lead with original currency, show MMK in parentheses with the snapshotted rate. |
| **F15** | What if a currency is deactivated while a draft is in flight? | Draft cannot be submitted until the user changes to an active currency. (Enforced in `SubmitRequestCommand`.) |
| **F18** | Should month boundaries use UTC or Myanmar local time (UTC+6:30)? | UTC for v1. Revisit if month-end edge cases bite. |
| **F19** | Should the monthly-spend display on the form refresh live? | Yes — fetch on department change and amount change. |

These all have safe defaults. Proceeding with these unless you object.

---

## 9. Notification Triggers (Slack)

| Event | Recipient |
|---|---|
| Submitted (regular) | Dept Head |
| Submitted (dept head's own request, under limit) | Finance directly |
| Submitted (dept head's own request, over limit) | Boss |
| Submitted (boss's own request) | Their Dept Head |
| Dept Head approves | Boss (if over limit) OR Finance |
| Dept Head rejects | Requester |
| Boss approves | Finance |
| Boss rejects | Requester (CC: Dept Head) |
| Finance approves | Requester |
| Finance marks paid | Requester |
| Finance sends back | Requester |

---

## 10. Key Implementation Notes

These flow from the rules above and shape the Domain layer:

1. **`BudgetRequest` aggregate** owns its workflow. State transitions are methods on the entity (`Submit()`, `ApproveBy(user)`, `Reject(user, comment)`), not setters. This keeps invariants in one place.

2. **Stage routing logic** lives in the Domain when computing the *next* status from the current one. Application layer just orchestrates (load → call method → persist → notify).

3. **Auto-approval is computed at `Submit()`-time**, not on demand. The flow is "fast-forwarded" through any stages where requester == approver, with corresponding `AutoApproved` `ApprovalAction` rows written in the same transaction.

4. **`DepartmentLimitSnapshot`** — store the limit value on the `BudgetRequest` row at submission, so audit and re-display are stable even if the department's limit changes later. Same applies to `ExchangeRateAtSubmission`, `RequestedAmountInMmkAtSubmission`, `MonthlyLimitAtSubmission`, and `MonthlySpendBeforeAtSubmission`.

5. **`AssignedApproverId` per stage** — also snapshot at submission, for the same reason (and for F12).

6. **Currency is a separate aggregate.** `BudgetRequest` references it by `CurrencyCode` (string FK); there is no navigation property between them. Currency rate changes are tracked in a dedicated `CurrencyRateChange` audit table written from the `UpdateCurrencyCommand` handler.

7. **Monthly-spend aggregate query lives in the repository.** The Application layer doesn't reach into EF directly; it calls `IBudgetRequestRepository.GetMonthlyApprovedSpendInMmkAsync(deptId, year, month, ct)`. The Domain `Submit()` method takes the pre-computed spend value as a parameter — it doesn't query for itself.

---

## 11. Currency Management

- Admin-only UI at `/admin/currencies` for listing, creating, and editing currencies, plus viewing per-currency rate history.
- Creating a currency requires Code, Name, and an initial RateToMmk.
- Editing supports Name, RateToMmk, and IsActive toggle. MMK is locked from rate / active changes.
- Any rate change writes a `CurrencyRateChange` row (old → new, changed-by, timestamp) in the same transaction as the rate update.
- The New Request form shows a live MMK conversion preview ("≈ X MMK at current rate") so requesters see the limit-comparison value before submitting. Rates are read live until the moment of `Submit()`, at which point they are snapshotted.

---

## 12. Next Step

Confirm:
- The 10K vs 100K example in chat was a typo and the limit value is one consistent number per department.
- The §8 defaults are acceptable.

Then we proceed to building the Domain layer.
