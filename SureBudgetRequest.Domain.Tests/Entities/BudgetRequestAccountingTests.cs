using FluentAssertions;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;
using Xunit;

namespace SureBudgetRequest.Domain.Tests.Entities;

public class BudgetRequestAccountingTests
{
    private static BudgetRequest CreateApprovedRequest()
    {
        var reqId = Guid.NewGuid();
        var headId = Guid.NewGuid();
        var request = BudgetRequest.CreateDraft(
            reqId, "Alice", BudgetRequestType.Standard, headId, "Bob Head",
            DateTime.UtcNow.AddDays(2), 100_000m, "MMK", "Reasons", "Alice", "Job", Guid.NewGuid(),
            false, null);

        request.Submit(
            Guid.NewGuid(), 500_000m, 10_000_000m, 0m,
            DateTime.UtcNow, DateTime.UtcNow.AddMonths(1),
            1m, headId, "Bob Head", "Alice", false, 1);

        request.ApproveBy(headId);
        request.ApproveBy(Guid.NewGuid(), Guid.NewGuid(), null);

        return request;
    }

    [Fact]
    public void SetAccountingCategory_WhenDraft_ShouldFail()
    {
        var request = BudgetRequest.CreateDraft(
            Guid.NewGuid(), "Alice", BudgetRequestType.Standard, Guid.NewGuid(), "Bob Head",
            DateTime.UtcNow.AddDays(2), 100_000m, "MMK", "Reasons", "Alice", "Job", Guid.NewGuid(),
            false, null);

        var result = request.SetAccountingCategory(Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("Accounting category can only be assigned after the request is approved.");
        request.AccountingCategoryId.Should().BeNull();
    }

    [Fact]
    public void SetAccountingCategory_WhenApproved_ShouldSucceed()
    {
        var request = CreateApprovedRequest();
        var categoryId = Guid.NewGuid();

        var result = request.SetAccountingCategory(categoryId);

        result.IsSuccess.Should().BeTrue();
        request.AccountingCategoryId.Should().Be(categoryId);
    }

    [Fact]
    public void SetAccountingCategory_WhenActorProvided_RecordsApprovalAction()
    {
        var request = CreateApprovedRequest();
        var categoryId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var fallbackUserId = Guid.NewGuid();

        var result = request.SetAccountingCategory(categoryId, actorId, "Office Supply", fallbackUserId);

        result.IsSuccess.Should().BeTrue();
        request.AccountingCategoryId.Should().Be(categoryId);
        request.ApprovalActions.Should().Contain(a =>
            a.Stage == ApprovalStage.Accounting &&
            a.Decision == ApprovalDecision.Categorized &&
            a.ApproverId == actorId &&
            a.Comment == "Accounting Category: Office Supply" &&
            a.FallbackUserId == fallbackUserId);
    }

    [Fact]
    public void RecordPrint_ShouldRecordTimestampAndUser()
    {
        var request = CreateApprovedRequest();
        var userId = Guid.NewGuid();

        var result = request.RecordPrint(userId);

        result.IsSuccess.Should().BeTrue();
        request.FirstPrintedAt.Should().NotBeNull();
        request.FirstPrintedByUserId.Should().Be(userId);

        var originalPrintTime = request.FirstPrintedAt!.Value;

        // Subsequent print should not overwrite first print timestamp
        var anotherUser = Guid.NewGuid();
        var result2 = request.RecordPrint(anotherUser);
        result2.IsSuccess.Should().BeTrue();
        request.FirstPrintedAt.Should().Be(originalPrintTime);
        request.FirstPrintedByUserId.Should().Be(userId);
    }

    [Fact]
    public void SetAccountingCategory_AfterPrinted_ShouldFailWithAuditLockError()
    {
        var request = CreateApprovedRequest();
        var initialCategory = Guid.NewGuid();
        request.SetAccountingCategory(initialCategory).IsSuccess.Should().BeTrue();

        // Print voucher
        request.RecordPrint(Guid.NewGuid()).IsSuccess.Should().BeTrue();
        request.FirstPrintedAt.Should().NotBeNull();

        // Attempt to change category after print
        var newCategory = Guid.NewGuid();
        var result = request.SetAccountingCategory(newCategory);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BudgetRequestErrors.AccountingCategoryLockedAfterPrint);
        request.AccountingCategoryId.Should().Be(initialCategory);
    }

    [Fact]
    public void AccountingCategory_Lifecycle_CreateRenameDeactivateReactivate()
    {
        var cat = new AccountingCategory("Office Supply");
        cat.Name.Should().Be("Office Supply");
        cat.IsActive.Should().BeTrue();

        cat.Rename("Office Supplies");
        cat.Name.Should().Be("Office Supplies");

        cat.Deactivate();
        cat.IsActive.Should().BeFalse();

        cat.Reactivate();
        cat.IsActive.Should().BeTrue();
    }
}
