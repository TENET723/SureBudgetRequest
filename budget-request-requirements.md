# Budget Request — Requirements Spec (v2)

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
- **Department** — has one Department Head and one budget limit (always denominated in MMK)
- **BudgetRequest** — the request itself, with status, amount, requester, **currency**
- **ApprovalAction** — audit row for every approve/reject/auto-approve decision
- **Attachment** — supporting documents
- **Currency** — master record (Code, Name, RateToMmk, IsActive). MMK exists as a row with rate = 1.
- **CurrencyRateChange** — audit row written every time an admin changes a currency's rate

## 4. Approval Flow

The flow is **strictly sequential**: Department Head → Boss (if applicable) → Finance.

Whether the Boss stage applies is determined by **the request amount (converted to MMK) vs. the department's budget limit at submission time**:

- amount-in-MMK **≤ limit** → skip Boss stage
- amount-in-MMK **> limit** → include Boss stage

When the request's currency is not MMK, the system converts the requested amount to MMK using the **exchange rate at submission time** (snapshotted onto the request — see R7 and R7a). Department limits are always denominated in MMK.

### 4.1 Auto-approval rule (handles self-approval cleanly)

> **If the assigned approver of a stage is the same person as the requester, that stage is automatically approved by the system.**

This single rule handles every self-approval case:

- A Department Head submits → their own dept head stage auto-approves
- The Boss submits → the boss stage auto-approves
- A person who is both Dept Head and Boss → both stages auto-approve (edge case, but supported)

The auto-approval is recorded in `ApprovalAction` with `Decision = AutoApproved` and `ApproverId = RequesterId` for full audit visibility.

### 4.2 Worked examples

**Case A — Regular employee, under limit:**
```
Submitted → PendingDeptHead (manual) → PendingFinance (manual) → Approved → Paid
```

**Case B — Regular employee, over limit:**
```
Submitted → PendingDeptHead (manual) → PendingBoss (manual) → PendingFinance (manual) → Approved → Paid
```

**Case C — Dept Head submits, under limit:**
```
Submitted → DeptHead AUTO-APPROVED → PendingFinance (manual) → Approved → Paid
```

**Case D — Dept Head submits, over limit:**
```
Submitted → DeptHead AUTO-APPROVED → PendingBoss (manual) → PendingFinance (manual) → Approved → Paid
```

**Case E — Boss submits, over their own dept's limit:**
```
Submitted → PendingDeptHead (manual) → Boss AUTO-APPROVED → PendingFinance (manual) → Approved → Paid
```

### 4.3 Rejection

Any approver can reject at their stage:

```
Pending<Stage> → Rejected
```

`Rejected` is terminal. To resubmit, the employee creates a new request (optionally linked to the rejected one for traceability).

## 5. Status Values

- `Draft` — saved but not submitted
- `PendingDeptHead`
- `PendingBoss` — only when amount-in-MMK > department limit
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
- **R6** — Department budget limit is a **per-request threshold** (in MMK), not cumulative monthly/yearly. A 1.5M-MMK-equivalent request triggers boss approval; a 0.9M-MMK-equivalent does not. The system does not track total spending.
- **R7** — The limit comparison uses the department's **current** limit AND the **current exchange rate** at submission time. Both are snapshotted onto the request. Future limit or rate changes do not retroactively re-route in-flight requests.
- **R7a** — Exchange rate snapshot fields on `BudgetRequest`: `CurrencyCode` (also stored from draft creation), `ExchangeRateAtSubmission`, `RequestedAmountInMmkAtSubmission`. These are immutable once set.
- **R8** — Approval is **strictly sequential**: Dept Head → Boss (if applicable) → Finance. No parallel approvals.
- **R9** — **Auto-approval rule**: any stage where the assigned approver is the requester is auto-approved by the system at submission time.
- **R10** — All status transitions (manual approvals, rejections, auto-approvals) write an `ApprovalAction` row.
- **R11** — Slack notifications fire on every status change.
- **R12** — All **currency rate changes** by admin write a `CurrencyRateChange` audit row in the same transaction (old rate, new rate, changed-by, timestamp).
- **R13** — MMK is locked: its rate cannot be changed and it cannot be deactivated.
- **R14** — A request in `SentBack` status that is resubmitted picks up the **then-current** exchange rate (rate is re-snapshotted, not preserved from the original submission), matching how the dept head / limit are re-evaluated.

---

## 7. Resolved Decisions

These were open in v1 and are now decided:

| ID | Decision |
|---|---|
| F1 — Who is Boss | Single user company-wide with Boss role |
| F2 — Self-approval | Auto-approve any stage where approver = requester (R9) |
| F3 — Limit period | Per-request threshold (not cumulative) |
| F4 — Approval order | Sequential: Dept Head → Boss → Finance |
| F5 — Race condition | Not applicable (not cumulative) |
| F13 — Multi-currency | Supported as of v2. See R1, R1a, R7, R7a, R12, R13, R14. |

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

4. **`DepartmentLimitSnapshot`** — store the limit value on the `BudgetRequest` row at submission, so audit and re-display are stable even if the department's limit changes later. Same applies to `ExchangeRateAtSubmission` and `RequestedAmountInMmkAtSubmission`.

5. **`AssignedApproverId` per stage** — also snapshot at submission, for the same reason (and for F12).

6. **Currency is a separate aggregate.** `BudgetRequest` references it by `CurrencyCode` (string FK); there is no navigation property between them. Currency rate changes are tracked in a dedicated `CurrencyRateChange` audit table written from the `UpdateCurrencyCommand` handler.

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
