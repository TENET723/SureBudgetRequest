# Project Rules & Development Guidelines

These rules govern all AI-assisted work in this repository (`SureBudgetRequest`). Every feature addition, modification, and refactoring must strictly adhere to these principles.

---

## 1. First-Principles Thinking
Whenever approaching a new feature, bug fix, architectural change, or refactoring:
- **Deconstruct to Core Fundamentals**: Break the problem down to its essential business requirements and basic truths. Strip away unwarranted assumptions, unnecessary conventions, and inherited habits.
- **Reason from the Ground Up**: Construct solutions based on fundamental domain needs rather than analogy, blind pattern-matching, or boilerplate replication.
- **Challenge Premises**: Before writing code, ask: *What is the fundamental problem being solved? What is the most direct, elegant way to solve it?*

---

## 2. Radical Simplicity ("As Simple as Possible")
Simplicity is a primary design constraint:
- **KISS & YAGNI**: Implement the simplest viable solution that completely and correctly satisfies the requirement. Avoid speculative generalizations, premature optimizations, and features not currently needed.
- **Minimize Indirection**: Avoid adding unnecessary abstraction layers, interfaces with only one trivial implementation, redundant wrappers, or excessive mapping layers unless strictly justified by domain boundaries or testability.
- **Prune & Consolidate**: When refactoring, actively favor deleting dead code, reducing complexity, and making logic flows transparent, readable, and maintainable.
- **Code Clarity Over Cleverness**: Code must be straightforward, predictable, and easy for any engineer to read and reason about.

---

## 3. Mandatory Plan-and-Approve Workflow
**Do not take action until the user approves the plan.**

Before modifying, creating, or deleting code, or making structural changes for any feature or refactoring:
1. **First-Principles Breakdown**: Clarify the core requirement, constraints, and why the proposed approach solves the problem.
2. **Present the Simplest Plan**: Present a clear, concise step-by-step implementation plan highlighting the minimal necessary changes.
3. **Wait for Explicit Approval**: Do not modify files, execute implementation commands, or take action until the user reviews and explicitly approves the plan.
4. **Execute & Verify**: Once approved, execute the plan cleanly and verify with targeted checks.

---

## 4. Targeted Verification (No Wasteful Test Runs)
- **UI / Presentation-Only Changes**: When changes are strictly confined to the Web / UI layer (`SureBudgetRequest.Web`, Razor components, UI services, CSS) and do not touch `Domain`, `Application`, or `Infrastructure`, do **not** run the unit test projects. Verifying compilation of `SureBudgetRequest.Web` is sufficient.
- **Backend / Domain Changes**: Only run the automated test suite (`dotnet test`) when modifying code in `Domain`, `Application`, or `Infrastructure`.

---

## 5. Global Table Design System & Ground Rules
All tables across the application must strictly adhere to the unified design standard established in `BudgetRequests.razor`:

1. **Row Number Column (`NO.`)**:
   - Every table must have a row number column.
   - Column header name must strictly be **`NO.`** (uppercase with period).
   - Use `class="sticky-no-col font-bold"` on `<th>` and `class="sticky-no-col text-muted text-sm text-center"` on `<td>`.

2. **Table Footer & Pagination Order**:
   - Every full table must have a footer (except embedded detail/modal sub-tables).
   - **Left side**: Optional / contextual (e.g. current page totals, subtotal, or item count).
   - **Right side**: Strict order matching `BudgetRequests`:
     `‹ Prev` button &rarr; `Page X of Y` (`text-sm text-muted`) &rarr; `Next ›` button &rarr; `Per page` dropdown (`[20 | 50 | 100]` with `ml-3`).

3. **Standardized Column Widths & CSS Classes**:
   - Reference standard from `app.css`:
     - `.sticky-no-col`: 44px (sticky left)
     - `.col-ref-date`: min-width 160px
     - `.col-requester`: min-width 175px
     - `.col-reason`: min-width 180px, max-width 240px
     - `.col-tags`: min-width 160px
     - `.col-requested`: min-width 145px (tabular numbers, right-aligned)
     - `.col-mmk`: min-width 130px (tabular numbers, right-aligned)
     - `.col-status`: min-width 175px (compact 22px chips)
     - `.col-coa`: min-width 95px
     - `.col-withdraw`: min-width 155px
     - `.sticky-action-col`: 48px (sticky right, 3-dots hover action)

4. **Table Control (`<AppTable>`)**:
   - All primary tables should use `<AppTable>` for consistent macOS window chrome, scrollbars, skeleton loading, and sticky layout.

