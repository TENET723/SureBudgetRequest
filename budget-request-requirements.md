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
| **Admin** | Manages users, departments, and budget limits. |

## 3. Core Entities

- **User** — belongs to a Department, has a Role
- **Department** — has one Department Head and one budget limit (in MMK)
- **BudgetRequest** — the request itself, with status, amount, requester
- **ApprovalAction** — audit row for every approve/reject/auto-approve decision
- **Attachment** — supporting documents

## 4. Approval Flow

The flow is **strictly sequential**: Department Head → Boss (if applicable) → Finance.

Whether the Boss stage applies is determined by **the request amount vs. the department's budget limit at submission time**:

- amount **≤ limit** → skip Boss stage
- amount **> limit** → include Boss stage

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
- `PendingBoss` — only when amount > department limit
- `PendingFinance`
- `Approved` — finance approved; awaiting payment dispatch
- `Paid` — finance has marked it paid
- `Rejected` — terminal
- `Cancelled` — withdrawn by requester before any approval

(`Submitted` is transient — submission moves directly into `PendingDeptHead` or auto-approves through it.)

## 6. Business Rules

- **R1** — Currency is **MMK** only (v1)
- **R2** — A request requires: amount, reason, requester, request date
- **R3** — Each department has exactly **one** Department Head
- **R4** — Each user belongs to exactly **one** department
- **R5** — There is exactly **one** Boss role-holder in the system at a time
- **R6** — Department budget limit is a **per-request threshold**, not cumulative monthly/yearly. A 1.5M request triggers boss approval; a 0.9M request does not. The system does not track total spending.
- **R7** — The limit comparison uses the department's **current** limit at submission time. Future limit changes do not retroactively re-route in-flight requests.
- **R8** — Approval is **strictly sequential**: Dept Head → Boss (if applicable) → Finance. No parallel approvals.
- **R9** — **Auto-approval rule**: any stage where the assigned approver is the requester is auto-approved by the system at submission time.
- **R10** — All status transitions (manual approvals, rejections, auto-approvals) write an `ApprovalAction` row.
- **R11** — Slack notifications fire on every status change.

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

4. **`DepartmentLimitSnapshot`** — store the limit value on the `BudgetRequest` row at submission, so audit and re-display are stable even if the department's limit changes later.

5. **`AssignedApproverId` per stage** — also snapshot at submission, for the same reason (and for F12).

---

## 11. Next Step

Confirm:
- The 10K vs 100K example in chat was a typo and the limit value is one consistent number per department.
- The §8 defaults are acceptable.

Then we proceed to building the Domain layer.
