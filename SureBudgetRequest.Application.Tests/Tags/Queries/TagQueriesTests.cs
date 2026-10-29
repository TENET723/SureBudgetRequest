using System.Text.Json;
using FluentAssertions;
using NSubstitute;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.Tags.Models;
using SureBudgetRequest.Application.Tags.Queries;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;
using Xunit;

namespace SureBudgetRequest.Application.Tests.Tags.Queries;

public class TagQueriesTests
{
    private readonly ITagRepository _tagRepository = Substitute.For<ITagRepository>();
    private readonly IBudgetRequestRepository _requestRepository = Substitute.For<IBudgetRequestRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IDepartmentRepository _departmentRepository = Substitute.For<IDepartmentRepository>();
    private readonly IAppSettingRepository _appSettingRepository = Substitute.For<IAppSettingRepository>();

    private readonly Guid _tagId = Guid.NewGuid();
    private readonly Guid _itDeptId = Guid.NewGuid();
    private readonly Guid _financeDeptId = Guid.NewGuid();
    private readonly Guid _managementDeptId = Guid.NewGuid();
    private readonly Guid _otherDeptId = Guid.NewGuid();

    private readonly Guid _requestId = Guid.NewGuid();
    private readonly Guid _requesterId = Guid.NewGuid();

    private Tag CreateTag(Guid deptId, string name, string? color = "#3B82F6")
    {
        var tag = new Tag(deptId, name, Guid.NewGuid(), color);
        typeof(Tag).GetProperty(nameof(Tag.Id))!.SetValue(tag, Guid.NewGuid());
        return tag;
    }

    private User CreateViewer(Guid deptId, UserRole role)
    {
        var user = new User("viewer", "Viewer", "viewer@test.com", deptId, role);
        typeof(User).GetProperty(nameof(User.Id))!.SetValue(user, Guid.NewGuid());
        return user;
    }

    private Department CreateDepartment(Guid id, string name)
    {
        var dept = new Department(name, Guid.NewGuid(), 100_000m, 500_000m);
        typeof(Department).GetProperty(nameof(Department.Id))!.SetValue(dept, id);
        return dept;
    }

    [Fact]
    public async Task GetTagQuery_ShouldReturnFailure_WhenTagNotFound()
    {
        _tagRepository.GetByIdAsync(_tagId, Arg.Any<CancellationToken>()).Returns((Tag?)null);

        var handler = new GetTagQueryHandler(_tagRepository);
        var result = await handler.Handle(new GetTagQuery(_tagId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(TagErrors.NotFound.Code);
    }

    [Fact]
    public async Task GetTagQuery_ShouldReturnTagDto_WhenTagExists()
    {
        var tag = CreateTag(_itDeptId, "Urgent", "#EF4444");
        _tagRepository.GetByIdAsync(_tagId, Arg.Any<CancellationToken>()).Returns(tag);

        var handler = new GetTagQueryHandler(_tagRepository);
        var result = await handler.Handle(new GetTagQuery(_tagId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Urgent");
        result.Value.ColorHex.Should().Be("#EF4444");
        result.Value.DepartmentId.Should().Be(_itDeptId);
    }

    [Fact]
    public async Task ListTagsByDepartmentQuery_ShouldReturnDepartmentTags()
    {
        var tags = new List<Tag>
        {
            CreateTag(_itDeptId, "Tag1"),
            CreateTag(_itDeptId, "Tag2")
        };
        _tagRepository.ListByDepartmentAsync(_itDeptId, false, Arg.Any<CancellationToken>()).Returns(tags);

        var handler = new ListTagsByDepartmentQueryHandler(_tagRepository);
        var result = await handler.Handle(new ListTagsByDepartmentQuery(_itDeptId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Select(t => t.Name).Should().Contain(new[] { "Tag1", "Tag2" });
    }

    [Fact]
    public async Task GetRequestTagsQuery_ShouldReturnFailure_WhenRequestNotFound()
    {
        _requestRepository.GetByIdAsync(_requestId, Arg.Any<CancellationToken>()).Returns((BudgetRequest?)null);

        var handler = new GetRequestTagsQueryHandler(_requestRepository, _userRepository, _departmentRepository, _appSettingRepository);
        var result = await handler.Handle(new GetRequestTagsQuery(_requestId, Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(BudgetRequestErrors.NotFound(_requestId).Code);
    }

    [Fact]
    public async Task GetRequestTagsQuery_ShouldFilterTags_BasedOnAppSettingConfigAndRequesterRole()
    {
        // Setup departments
        var itDept = CreateDepartment(_itDeptId, "IT");
        var finDept = CreateDepartment(_financeDeptId, "Finance Dept");
        var mgmtDept = CreateDepartment(_managementDeptId, "Executive Management");
        var otherDept = CreateDepartment(_otherDeptId, "Marketing");
        _departmentRepository.ListAsync(true, Arg.Any<CancellationToken>())
            .Returns(new[] { itDept, finDept, mgmtDept, otherDept });

        // Setup AppSetting with explicit department IDs and matrix
        var config = new TagVisibilityConfig
        {
            FinanceDepartmentId = _financeDeptId,
            ManagementDepartmentId = _managementDeptId,
            RoleVisibilityMatrix = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Requester"] = new() { "Management", "Finance" },
                ["Management"] = new() { "Requester" },
                ["Finance"] = new(),
                ["Employee"] = new()
            }
        };
        var setting = new AppSetting(TagVisibilityConfig.SettingKey, JsonSerializer.Serialize(config));
        _appSettingRepository.GetByKeyAsync(TagVisibilityConfig.SettingKey, Arg.Any<CancellationToken>()).Returns(setting);

        // Setup requester (IT Employee)
        var requester = CreateViewer(_itDeptId, UserRole.Employee);
        typeof(User).GetProperty(nameof(User.Id))!.SetValue(requester, _requesterId);
        _userRepository.GetByIdAsync(_requesterId, Arg.Any<CancellationToken>()).Returns(requester);

        // Setup request for IT department
        var request = BudgetRequest.CreateDraft(
            _requesterId, "Requester", BudgetRequestType.Standard, Guid.NewGuid(), "Head",
            DateTime.UtcNow.AddDays(1), 100_000m, "MMK", "Reason", "Withdrawer", "Title",
            Guid.NewGuid(), false, null);
        typeof(BudgetRequest).GetProperty(nameof(BudgetRequest.Id))!.SetValue(request, _requestId);
        typeof(BudgetRequest).GetProperty(nameof(BudgetRequest.DepartmentIdAtSubmission))!.SetValue(request, _itDeptId);

        // Attach tags from IT, Finance, Management, and Marketing
        var itTag = CreateTag(_itDeptId, "IT-Tag");
        var finTag = CreateTag(_financeDeptId, "Finance-Tag");
        var mgmtTag = CreateTag(_managementDeptId, "Management-Tag");
        var otherTag = CreateTag(_otherDeptId, "Marketing-Tag");

        request.AddTag(itTag, _requesterId, _itDeptId);
        request.AddTag(finTag, Guid.NewGuid(), _financeDeptId);
        request.AddTag(mgmtTag, Guid.NewGuid(), _managementDeptId);
        request.AddTag(otherTag, Guid.NewGuid(), _otherDeptId);

        _requestRepository.GetByIdAsync(_requestId, Arg.Any<CancellationToken>()).Returns(request);
        var handler = new GetRequestTagsQueryHandler(_requestRepository, _userRepository, _departmentRepository, _appSettingRepository);

        // Case 1: IT Employee (Requester) -> sees IT (own/requester), Management, and Finance tags; does NOT see Marketing
        var itViewer = CreateViewer(_itDeptId, UserRole.Employee);
        _userRepository.GetByIdAsync(itViewer.Id, Arg.Any<CancellationToken>()).Returns(itViewer);
        var itResult = await handler.Handle(new GetRequestTagsQuery(_requestId, itViewer.Id), CancellationToken.None);
        itResult.Value.Select(t => t.Name).Should().BeEquivalentTo(new[] { "IT-Tag", "Management-Tag", "Finance-Tag" });

        // Case 2: Management Viewer -> sees Management (own) + IT (requester); does NOT see Finance or Marketing
        var mgmtViewer = CreateViewer(_managementDeptId, UserRole.Management);
        _userRepository.GetByIdAsync(mgmtViewer.Id, Arg.Any<CancellationToken>()).Returns(mgmtViewer);
        var mgmtResult = await handler.Handle(new GetRequestTagsQuery(_requestId, mgmtViewer.Id), CancellationToken.None);
        mgmtResult.Value.Select(t => t.Name).Should().BeEquivalentTo(new[] { "Management-Tag", "IT-Tag" });

        // Case 3: Finance Viewer -> sees Finance (own) only; cannot see IT, Management, or Marketing
        var finViewer = CreateViewer(_financeDeptId, UserRole.Finance);
        _userRepository.GetByIdAsync(finViewer.Id, Arg.Any<CancellationToken>()).Returns(finViewer);
        var finResult = await handler.Handle(new GetRequestTagsQuery(_requestId, finViewer.Id), CancellationToken.None);
        finResult.Value.Select(t => t.Name).Should().ContainSingle().Which.Should().Be("Finance-Tag");

        // Case 4: Admin Viewer -> sees ALL tags
        var adminViewer = CreateViewer(Guid.NewGuid(), UserRole.Admin);
        _userRepository.GetByIdAsync(adminViewer.Id, Arg.Any<CancellationToken>()).Returns(adminViewer);
        var adminResult = await handler.Handle(new GetRequestTagsQuery(_requestId, adminViewer.Id), CancellationToken.None);
        adminResult.Value.Should().HaveCount(4);
    }

    [Fact]
    public async Task GetRequestTagsQuery_FinanceAsRequester_ShouldApplyFinancePolicy_AndNotSeeManagementTags()
    {
        // Setup departments
        var finDept = CreateDepartment(_financeDeptId, "Finance Dept");
        var mgmtDept = CreateDepartment(_managementDeptId, "Management Dept");
        _departmentRepository.ListAsync(true, Arg.Any<CancellationToken>()).Returns(new[] { finDept, mgmtDept });

        var config = new TagVisibilityConfig
        {
            FinanceDepartmentId = _financeDeptId,
            ManagementDepartmentId = _managementDeptId,
            RoleVisibilityMatrix = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Requester"] = new() { "Management", "Finance" },
                ["Finance"] = new() // Finance is restricted from seeing Management tags
            }
        };
        var setting = new AppSetting(TagVisibilityConfig.SettingKey, JsonSerializer.Serialize(config));
        _appSettingRepository.GetByKeyAsync(TagVisibilityConfig.SettingKey, Arg.Any<CancellationToken>()).Returns(setting);

        // Setup Finance user as Requester
        var financeRequester = CreateViewer(_financeDeptId, UserRole.Finance);
        typeof(User).GetProperty(nameof(User.Id))!.SetValue(financeRequester, _requesterId);
        _userRepository.GetByIdAsync(_requesterId, Arg.Any<CancellationToken>()).Returns(financeRequester);

        // Request submitted by Finance
        var request = BudgetRequest.CreateDraft(
            _requesterId, "Finance Officer", BudgetRequestType.Standard, Guid.NewGuid(), "Finance Head",
            DateTime.UtcNow.AddDays(1), 50_000m, "MMK", "Audit supplies", "Withdrawer", "Title",
            Guid.NewGuid(), false, null);
        typeof(BudgetRequest).GetProperty(nameof(BudgetRequest.Id))!.SetValue(request, _requestId);
        typeof(BudgetRequest).GetProperty(nameof(BudgetRequest.DepartmentIdAtSubmission))!.SetValue(request, _financeDeptId);

        var finTag = CreateTag(_financeDeptId, "Finance-Tag");
        var mgmtTag = CreateTag(_managementDeptId, "Management-Tag");
        request.AddTag(finTag, _requesterId, _financeDeptId);
        request.AddTag(mgmtTag, Guid.NewGuid(), _managementDeptId);

        _requestRepository.GetByIdAsync(_requestId, Arg.Any<CancellationToken>()).Returns(request);
        var handler = new GetRequestTagsQueryHandler(_requestRepository, _userRepository, _departmentRepository, _appSettingRepository);

        // When the Finance Requester views their own request:
        // Because their role is Finance, they are evaluated under "Finance" policy ([]), NOT generic "Requester" policy!
        var result = await handler.Handle(new GetRequestTagsQuery(_requestId, financeRequester.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Select(t => t.Name).Should().ContainSingle().Which.Should().Be("Finance-Tag");
    }

    [Fact]
    public async Task GetRequestTagsQuery_MissingAppSetting_ShouldFallbackToDefaultsGracefully()
    {
        var itDept = CreateDepartment(_itDeptId, "IT");
        var finDept = CreateDepartment(_financeDeptId, "Finance");
        _departmentRepository.ListAsync(true, Arg.Any<CancellationToken>()).Returns(new[] { itDept, finDept });

        // AppSetting returns null
        _appSettingRepository.GetByKeyAsync(TagVisibilityConfig.SettingKey, Arg.Any<CancellationToken>()).Returns((AppSetting?)null);

        var requester = CreateViewer(_itDeptId, UserRole.Employee);
        typeof(User).GetProperty(nameof(User.Id))!.SetValue(requester, _requesterId);
        _userRepository.GetByIdAsync(_requesterId, Arg.Any<CancellationToken>()).Returns(requester);

        var request = BudgetRequest.CreateDraft(
            _requesterId, "Requester", BudgetRequestType.Standard, Guid.NewGuid(), "Head",
            DateTime.UtcNow.AddDays(1), 100_000m, "MMK", "Reason", "Withdrawer", "Title",
            Guid.NewGuid(), false, null);
        typeof(BudgetRequest).GetProperty(nameof(BudgetRequest.Id))!.SetValue(request, _requestId);
        typeof(BudgetRequest).GetProperty(nameof(BudgetRequest.DepartmentIdAtSubmission))!.SetValue(request, _itDeptId);

        var itTag = CreateTag(_itDeptId, "IT-Tag");
        request.AddTag(itTag, _requesterId, _itDeptId);

        _requestRepository.GetByIdAsync(_requestId, Arg.Any<CancellationToken>()).Returns(request);
        var handler = new GetRequestTagsQueryHandler(_requestRepository, _userRepository, _departmentRepository, _appSettingRepository);

        var itViewer = CreateViewer(_itDeptId, UserRole.Employee);
        _userRepository.GetByIdAsync(itViewer.Id, Arg.Any<CancellationToken>()).Returns(itViewer);

        var result = await handler.Handle(new GetRequestTagsQuery(_requestId, itViewer.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Select(t => t.Name).Should().ContainSingle().Which.Should().Be("IT-Tag");
    }

    [Fact]
    public async Task GetRequestTagsQuery_WhenDepartmentsNotSetInAppSetting_ShouldNotApplyCrossDepartmentReadPermissions()
    {
        var itDept = CreateDepartment(_itDeptId, "IT");
        var finDept = CreateDepartment(_financeDeptId, "Finance");
        var mgmtDept = CreateDepartment(_managementDeptId, "Management");
        _departmentRepository.ListAsync(true, Arg.Any<CancellationToken>()).Returns(new[] { itDept, finDept, mgmtDept });

        // AppSetting with null department IDs (no fallback to string names allowed)
        var config = new TagVisibilityConfig
        {
            FinanceDepartmentId = null,
            ManagementDepartmentId = null,
            RoleVisibilityMatrix = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Requester"] = new() { "Management", "Finance" }
            }
        };
        var setting = new AppSetting(TagVisibilityConfig.SettingKey, JsonSerializer.Serialize(config));
        _appSettingRepository.GetByKeyAsync(TagVisibilityConfig.SettingKey, Arg.Any<CancellationToken>()).Returns(setting);

        var requester = CreateViewer(_itDeptId, UserRole.Employee);
        typeof(User).GetProperty(nameof(User.Id))!.SetValue(requester, _requesterId);
        _userRepository.GetByIdAsync(_requesterId, Arg.Any<CancellationToken>()).Returns(requester);

        var request = BudgetRequest.CreateDraft(
            _requesterId, "Requester", BudgetRequestType.Standard, Guid.NewGuid(), "Head",
            DateTime.UtcNow.AddDays(1), 100_000m, "MMK", "Reason", "Withdrawer", "Title",
            Guid.NewGuid(), false, null);
        typeof(BudgetRequest).GetProperty(nameof(BudgetRequest.Id))!.SetValue(request, _requestId);
        typeof(BudgetRequest).GetProperty(nameof(BudgetRequest.DepartmentIdAtSubmission))!.SetValue(request, _itDeptId);

        var itTag = CreateTag(_itDeptId, "IT-Tag");
        var finTag = CreateTag(_financeDeptId, "Finance-Tag");
        var mgmtTag = CreateTag(_managementDeptId, "Management-Tag");
        request.AddTag(itTag, _requesterId, _itDeptId);
        request.AddTag(finTag, Guid.NewGuid(), _financeDeptId);
        request.AddTag(mgmtTag, Guid.NewGuid(), _managementDeptId);

        _requestRepository.GetByIdAsync(_requestId, Arg.Any<CancellationToken>()).Returns(request);
        var handler = new GetRequestTagsQueryHandler(_requestRepository, _userRepository, _departmentRepository, _appSettingRepository);

        var itViewer = CreateViewer(_itDeptId, UserRole.Employee);
        _userRepository.GetByIdAsync(itViewer.Id, Arg.Any<CancellationToken>()).Returns(itViewer);

        // IT Employee is Requester and matrix allows "Finance" and "Management",
        // BUT because FinanceDepartmentId and ManagementDepartmentId are null,
        // no tags are matched as Finance or Management.
        // Therefore, only own department tag (IT-Tag) is visible!
        var result = await handler.Handle(new GetRequestTagsQuery(_requestId, itViewer.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Select(t => t.Name).Should().ContainSingle().Which.Should().Be("IT-Tag");

        // Management Viewer: Even though matrix has "Requester", because departments are not configured,
        // the circuit breaker activates and Management can ONLY see their own department's tags!
        var mgmtViewer = CreateViewer(_managementDeptId, UserRole.Management);
        _userRepository.GetByIdAsync(mgmtViewer.Id, Arg.Any<CancellationToken>()).Returns(mgmtViewer);

        var mgmtResult = await handler.Handle(new GetRequestTagsQuery(_requestId, mgmtViewer.Id), CancellationToken.None);

        mgmtResult.IsSuccess.Should().BeTrue();
        mgmtResult.Value.Select(t => t.Name).Should().ContainSingle().Which.Should().Be("Management-Tag");
    }

    [Fact]
    public async Task GetRequestTagsQuery_ShouldGroupAndFilterAccountingTags_WhenConfigured()
    {
        var acctDeptId = Guid.NewGuid();
        var itDept = CreateDepartment(_itDeptId, "IT");
        var finDept = CreateDepartment(_financeDeptId, "Finance");
        var mgmtDept = CreateDepartment(_managementDeptId, "Management");
        var acctDept = CreateDepartment(acctDeptId, "Accounting");
        _departmentRepository.ListAsync(true, Arg.Any<CancellationToken>()).Returns(new[] { itDept, finDept, mgmtDept, acctDept });

        var config = new TagVisibilityConfig
        {
            FinanceDepartmentId = _financeDeptId,
            ManagementDepartmentId = _managementDeptId,
            AccountingDepartmentId = acctDeptId,
            RoleVisibilityMatrix = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Accounting"] = new() { "Requester", "Finance", "Management" },
                ["Requester"] = new() { "Accounting" }
            }
        };
        var setting = new AppSetting(TagVisibilityConfig.SettingKey, JsonSerializer.Serialize(config));
        _appSettingRepository.GetByKeyAsync(TagVisibilityConfig.SettingKey, Arg.Any<CancellationToken>()).Returns(setting);

        var request = BudgetRequest.CreateDraft(
            _requesterId, "Requester", BudgetRequestType.Standard, Guid.NewGuid(), "Head",
            DateTime.UtcNow.AddDays(1), 100_000m, "MMK", "Reason", "Withdrawer", "Title",
            Guid.NewGuid(), false, null);
        typeof(BudgetRequest).GetProperty(nameof(BudgetRequest.Id))!.SetValue(request, _requestId);
        typeof(BudgetRequest).GetProperty(nameof(BudgetRequest.DepartmentIdAtSubmission))!.SetValue(request, _itDeptId);

        var itTag = CreateTag(_itDeptId, "IT-Tag");
        var acctTag = CreateTag(acctDeptId, "Acct-Audit");
        var finTag = CreateTag(_financeDeptId, "Finance-Tag");
        request.AddTag(itTag, _requesterId, _itDeptId);
        request.AddTag(acctTag, Guid.NewGuid(), acctDeptId);
        request.AddTag(finTag, Guid.NewGuid(), _financeDeptId);

        _requestRepository.GetByIdAsync(_requestId, Arg.Any<CancellationToken>()).Returns(request);
        var handler = new GetRequestTagsQueryHandler(_requestRepository, _userRepository, _departmentRepository, _appSettingRepository);

        // Accounting viewer: should see Requester, Finance, and own Accounting tag
        var acctViewer = CreateViewer(acctDeptId, UserRole.Accounting);
        _userRepository.GetByIdAsync(acctViewer.Id, Arg.Any<CancellationToken>()).Returns(acctViewer);

        var acctResult = await handler.Handle(new GetRequestTagsQuery(_requestId, acctViewer.Id), CancellationToken.None);
        acctResult.IsSuccess.Should().BeTrue();
        acctResult.Value.Should().HaveCount(3);
        acctResult.Value.First(t => t.Name == "Acct-Audit").TagGroup.Should().Be("Accounting");

        // Requester viewer: should see own IT-Tag and Acct-Audit (since matrix has Accounting)
        var itViewer = CreateViewer(_itDeptId, UserRole.Employee);
        _userRepository.GetByIdAsync(itViewer.Id, Arg.Any<CancellationToken>()).Returns(itViewer);

        var itResult = await handler.Handle(new GetRequestTagsQuery(_requestId, itViewer.Id), CancellationToken.None);
        itResult.IsSuccess.Should().BeTrue();
        itResult.Value.Select(t => t.Name).Should().Contain(new[] { "IT-Tag", "Acct-Audit" });
    }

    [Fact]
    public async Task ListVisibleTagsForUserQuery_ShouldReturnFailure_WhenUserNotFound()
    {
        var missingUserId = Guid.NewGuid();
        _userRepository.GetByIdAsync(missingUserId, Arg.Any<CancellationToken>()).Returns((User?)null);

        var handler = new ListVisibleTagsForUserQueryHandler(_tagRepository, _userRepository, _departmentRepository);
        var result = await handler.Handle(new ListVisibleTagsForUserQuery(missingUserId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(UserErrors.NotFound(missingUserId).Code);
    }

    [Fact]
    public async Task ListVisibleTagsForUserQuery_Employee_ShouldOnlyReturnOwnDepartmentActiveTags()
    {
        var employee = CreateViewer(_itDeptId, UserRole.Employee);
        _userRepository.GetByIdAsync(employee.Id, Arg.Any<CancellationToken>()).Returns(employee);

        var itDept = CreateDepartment(_itDeptId, "IT");
        _departmentRepository.ListAsync(true, Arg.Any<CancellationToken>()).Returns(new[] { itDept });

        var itTag = CreateTag(_itDeptId, "hardware");
        _tagRepository.ListByDepartmentAsync(_itDeptId, false, Arg.Any<CancellationToken>()).Returns(new[] { itTag });

        var handler = new ListVisibleTagsForUserQueryHandler(_tagRepository, _userRepository, _departmentRepository);
        var result = await handler.Handle(new ListVisibleTagsForUserQuery(employee.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value![0].Name.Should().Be("hardware");
        result.Value[0].DepartmentName.Should().Be("IT");
        result.Value[0].DisplayName.Should().Be("#hardware");
        result.Value[0].FullDisplayName.Should().Be("[IT] #hardware");
    }

    [Fact]
    public async Task ListVisibleTagsForUserQuery_Finance_ShouldOnlyReturnFinanceDepartmentActiveTags()
    {
        var financeUser = CreateViewer(_financeDeptId, UserRole.Finance);
        _userRepository.GetByIdAsync(financeUser.Id, Arg.Any<CancellationToken>()).Returns(financeUser);

        var finDept = CreateDepartment(_financeDeptId, "Finance");
        _departmentRepository.ListAsync(true, Arg.Any<CancellationToken>()).Returns(new[] { finDept });

        var finTag = CreateTag(_financeDeptId, "tax-exempt");
        _tagRepository.ListByDepartmentAsync(_financeDeptId, false, Arg.Any<CancellationToken>()).Returns(new[] { finTag });

        var handler = new ListVisibleTagsForUserQueryHandler(_tagRepository, _userRepository, _departmentRepository);
        var result = await handler.Handle(new ListVisibleTagsForUserQuery(financeUser.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value![0].Name.Should().Be("tax-exempt");
        result.Value[0].DepartmentName.Should().Be("Finance");
    }

    [Fact]
    public async Task ListVisibleTagsForUserQuery_Management_ShouldOnlyReturnManagementDepartmentActiveTags()
    {
        var mgmtUser = CreateViewer(_managementDeptId, UserRole.Management);
        _userRepository.GetByIdAsync(mgmtUser.Id, Arg.Any<CancellationToken>()).Returns(mgmtUser);

        var mgmtDept = CreateDepartment(_managementDeptId, "Management");
        _departmentRepository.ListAsync(true, Arg.Any<CancellationToken>()).Returns(new[] { mgmtDept });

        var mgmtTag = CreateTag(_managementDeptId, "capex-approved");
        _tagRepository.ListByDepartmentAsync(_managementDeptId, false, Arg.Any<CancellationToken>()).Returns(new[] { mgmtTag });

        var handler = new ListVisibleTagsForUserQueryHandler(_tagRepository, _userRepository, _departmentRepository);
        var result = await handler.Handle(new ListVisibleTagsForUserQuery(mgmtUser.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value![0].Name.Should().Be("capex-approved");
        result.Value[0].DepartmentName.Should().Be("Management");
    }

    [Fact]
    public async Task ListVisibleTagsForUserQuery_Admin_ShouldReturnAllActiveTagsAcrossDepartments()
    {
        var admin = CreateViewer(Guid.NewGuid(), UserRole.Admin);
        _userRepository.GetByIdAsync(admin.Id, Arg.Any<CancellationToken>()).Returns(admin);

        var itDept = CreateDepartment(_itDeptId, "IT");
        var finDept = CreateDepartment(_financeDeptId, "Finance");
        _departmentRepository.ListAsync(true, Arg.Any<CancellationToken>()).Returns(new[] { itDept, finDept });

        var itTag = CreateTag(_itDeptId, "hardware");
        var finTag = CreateTag(_financeDeptId, "tax");
        _tagRepository.ListAllActiveAsync(Arg.Any<CancellationToken>()).Returns(new[] { itTag, finTag });

        var handler = new ListVisibleTagsForUserQueryHandler(_tagRepository, _userRepository, _departmentRepository);
        var result = await handler.Handle(new ListVisibleTagsForUserQuery(admin.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value!.Select(t => t.Name).Should().Contain(new[] { "hardware", "tax" });
    }
}
