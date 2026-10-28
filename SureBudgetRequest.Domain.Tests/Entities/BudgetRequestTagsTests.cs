using FluentAssertions;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;
using Xunit;

namespace SureBudgetRequest.Domain.Tests.Entities;

public class BudgetRequestTagsTests
{
    private readonly Guid _requestId = Guid.NewGuid();
    private readonly Guid _requesterId = Guid.NewGuid();
    private readonly Guid _deptHeadId = Guid.NewGuid();
    private readonly Guid _itDeptId = Guid.NewGuid();
    private readonly Guid _financeDeptId = Guid.NewGuid();
    private readonly Guid _withdrawMethodId = Guid.NewGuid();

    private BudgetRequest CreateRequest()
    {
        var req = BudgetRequest.CreateDraft(
            _requesterId,
            "Alice Requester",
            BudgetRequestType.Standard,
            _deptHeadId,
            "Bob Head",
            DateTime.UtcNow.AddDays(1),
            100_000m,
            "MMK",
            "Hardware purchase",
            "U Ba",
            "Officer",
            _withdrawMethodId,
            false,
            null);

        typeof(BudgetRequest).GetProperty(nameof(BudgetRequest.Id))!.SetValue(req, _requestId);
        return req;
    }

    private Tag CreateTag(Guid deptId, string name = "SampleTag", bool isActive = true)
    {
        var tag = new Tag(deptId, name, Guid.NewGuid(), "#3B82F6");
        typeof(Tag).GetProperty(nameof(Tag.Id))!.SetValue(tag, Guid.NewGuid());
        if (!isActive)
        {
            tag.Deactivate();
        }
        return tag;
    }

    [Fact]
    public void AddTag_ShouldThrowArgumentNullException_WhenTagIsNull()
    {
        var request = CreateRequest();

        var act = () => request.AddTag(null!, _requesterId, _itDeptId);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddTag_ShouldReturnFailure_WhenTagIsInactive()
    {
        var request = CreateRequest();
        var inactiveTag = CreateTag(_itDeptId, "OldTag", isActive: false);

        var result = request.AddTag(inactiveTag, _requesterId, _itDeptId);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(TagErrors.Inactive("OldTag").Code);
        request.Tags.Should().BeEmpty();
    }

    [Fact]
    public void AddTag_ShouldReturnForbidden_WhenUserTriesToAttachTagFromAnotherDepartment()
    {
        var request = CreateRequest();
        var financeTag = CreateTag(_financeDeptId, "AuditQ4");

        // User belongs to IT department, but tries to attach Finance's tag
        var result = request.AddTag(financeTag, _requesterId, _itDeptId);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(TagErrors.CrossDepartmentNotAllowed.Code);
        request.Tags.Should().BeEmpty();
    }

    [Fact]
    public void AddTag_ShouldReturnFailure_WhenTagAlreadyAttached()
    {
        var request = CreateRequest();
        var tag = CreateTag(_itDeptId, "Hardware");

        var firstResult = request.AddTag(tag, _requesterId, _itDeptId);
        firstResult.IsSuccess.Should().BeTrue();

        var secondResult = request.AddTag(tag, _requesterId, _itDeptId);

        secondResult.IsFailure.Should().BeTrue();
        secondResult.Error.Code.Should().Be(TagErrors.AlreadyAttached("Hardware").Code);
        request.Tags.Should().HaveCount(1);
    }

    [Fact]
    public void AddTag_ShouldReturnFailure_WhenDepartmentExceedsMaxTagsLimit()
    {
        var request = CreateRequest();
        const int maxLimit = 3;

        for (int i = 1; i <= maxLimit; i++)
        {
            var tag = CreateTag(_itDeptId, $"Tag-{i}");
            var addRes = request.AddTag(tag, _requesterId, _itDeptId, maxLimit);
            addRes.IsSuccess.Should().BeTrue();
        }

        var fourthTag = CreateTag(_itDeptId, "Tag-4");
        var result = request.AddTag(fourthTag, _requesterId, _itDeptId, maxLimit);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(TagErrors.MaxDepartmentTagsReached(maxLimit).Code);
        request.Tags.Should().HaveCount(maxLimit);
    }

    [Fact]
    public void AddTag_ShouldAllowDifferentDepartmentsToHaveTheirOwnLimitsIndependently()
    {
        var request = CreateRequest();
        const int maxLimit = 2;

        // IT adds 2 tags
        var itTag1 = CreateTag(_itDeptId, "IT-1");
        var itTag2 = CreateTag(_itDeptId, "IT-2");
        request.AddTag(itTag1, _requesterId, _itDeptId, maxLimit).IsSuccess.Should().BeTrue();
        request.AddTag(itTag2, _requesterId, _itDeptId, maxLimit).IsSuccess.Should().BeTrue();

        // Finance user also adds 2 tags to the same request
        var financeUserId = Guid.NewGuid();
        var finTag1 = CreateTag(_financeDeptId, "Fin-1");
        var finTag2 = CreateTag(_financeDeptId, "Fin-2");
        request.AddTag(finTag1, financeUserId, _financeDeptId, maxLimit).IsSuccess.Should().BeTrue();
        request.AddTag(finTag2, financeUserId, _financeDeptId, maxLimit).IsSuccess.Should().BeTrue();

        request.Tags.Should().HaveCount(4);
    }

    [Fact]
    public void RemoveTag_ShouldReturnFailure_WhenTagNotAttached()
    {
        var request = CreateRequest();
        var unattachedTagId = Guid.NewGuid();

        var result = request.RemoveTag(unattachedTagId, _requesterId, _itDeptId);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(TagErrors.TagNotAttached.Code);
    }

    [Fact]
    public void RemoveTag_ShouldReturnForbidden_WhenUserAttemptsToRemoveAnotherDepartmentTag()
    {
        var request = CreateRequest();
        var financeUserId = Guid.NewGuid();
        var financeTag = CreateTag(_financeDeptId, "TaxExempt");

        request.AddTag(financeTag, financeUserId, _financeDeptId).IsSuccess.Should().BeTrue();

        // IT user tries to remove Finance's tag
        var result = request.RemoveTag(financeTag.Id, _requesterId, _itDeptId);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(TagErrors.CannotRemoveCrossDepartmentTag.Code);
        request.Tags.Should().HaveCount(1);
    }

    [Fact]
    public void RemoveTag_ShouldSucceed_WhenUserRemovesTheirOwnDepartmentTag()
    {
        var request = CreateRequest();
        var tag = CreateTag(_itDeptId, "RemovableTag");

        request.AddTag(tag, _requesterId, _itDeptId).IsSuccess.Should().BeTrue();
        request.Tags.Should().HaveCount(1);

        var result = request.RemoveTag(tag.Id, _requesterId, _itDeptId);

        result.IsSuccess.Should().BeTrue();
        request.Tags.Should().BeEmpty();
    }
}
