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
