using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.BudgetRequests.Commands.AddTagToRequest;
using SureBudgetRequest.Application.Tags.Models;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;
using Xunit;

namespace SureBudgetRequest.Application.Tests.BudgetRequests.Commands;

public class AddTagToRequestCommandHandlerTests
{
    private readonly IBudgetRequestRepository _requestRepository = Substitute.For<IBudgetRequestRepository>();
    private readonly ITagRepository _tagRepository = Substitute.For<ITagRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IDepartmentRepository _departmentRepository = Substitute.For<IDepartmentRepository>();
    private readonly IAppSettingRepository _appSettingRepository = Substitute.For<IAppSettingRepository>();
    private readonly IBudgetRequestModificationRepository _modificationRepository = Substitute.For<IBudgetRequestModificationRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private readonly AddTagToRequestCommandHandler _handler;

    private readonly Guid _requestId = Guid.NewGuid();
    private readonly Guid _tagId = Guid.NewGuid();
    private readonly Guid _deptId = Guid.NewGuid();
    private readonly Guid _actorId = Guid.NewGuid();

    public AddTagToRequestCommandHandlerTests()
    {
        _handler = new AddTagToRequestCommandHandler(
            _requestRepository,
            _tagRepository,
            _userRepository,
            _departmentRepository,
            _appSettingRepository,
            _modificationRepository,
            _unitOfWork);
    }

    private BudgetRequest CreateRequest()
    {
        var request = BudgetRequest.CreateDraft(
            _actorId,
            "Requester",
            BudgetRequestType.Standard,
            Guid.NewGuid(),
            "Head",
            DateTime.UtcNow.AddDays(1),
            100_000m,
            "MMK",
            "Hardware",
            "Withdrawer",
            "Title",
            Guid.NewGuid(),
            false,
            null);
        typeof(BudgetRequest).GetProperty(nameof(BudgetRequest.Id))!.SetValue(request, _requestId);
        return request;
    }

    private Tag CreateTag(bool isActive = true)
    {
        var tag = new Tag(_deptId, "Hardware", _actorId);
        typeof(Tag).GetProperty(nameof(Tag.Id))!.SetValue(tag, _tagId);
        if (!isActive) tag.Deactivate();
        return tag;
    }

    private User CreateUser(Guid deptId, bool isActive = true, UserRole role = UserRole.Employee)
    {
        var user = new User("actor", "Actor", "actor@test.com", deptId, role);
        typeof(User).GetProperty(nameof(User.Id))!.SetValue(user, _actorId);
        if (!isActive) user.Deactivate();
        return user;
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenBudgetRequestNotFound()
    {
        _requestRepository.GetByIdAsync(_requestId, Arg.Any<CancellationToken>()).Returns((BudgetRequest?)null);

        var command = new AddTagToRequestCommand(_requestId, _tagId, _actorId);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(BudgetRequestErrors.NotFound(_requestId).Code);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenActorNotFoundOrInactive()
    {
        var request = CreateRequest();
        _requestRepository.GetByIdAsync(_requestId, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(_actorId, Arg.Any<CancellationToken>()).Returns((User?)null);

        var command = new AddTagToRequestCommand(_requestId, _tagId, _actorId);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(UserErrors.NotFound(_actorId).Code);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenTagNotFound()
    {
        var request = CreateRequest();
        var user = CreateUser(_deptId);
        _requestRepository.GetByIdAsync(_requestId, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(_actorId, Arg.Any<CancellationToken>()).Returns(user);
        _tagRepository.GetByIdAsync(_tagId, Arg.Any<CancellationToken>()).Returns((Tag?)null);

        var command = new AddTagToRequestCommand(_requestId, _tagId, _actorId);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(TagErrors.NotFound.Code);
    }

    [Fact]
    public async Task Handle_ShouldReturnForbidden_WhenTagBelongsToAnotherDepartment()
    {
        var request = CreateRequest();
        var user = CreateUser(_deptId); // User in _deptId
        var tag = new Tag(Guid.NewGuid(), "FinanceTag", _actorId); // Tag in different dept
        typeof(Tag).GetProperty(nameof(Tag.Id))!.SetValue(tag, _tagId);

        _requestRepository.GetByIdAsync(_requestId, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(_actorId, Arg.Any<CancellationToken>()).Returns(user);
        _tagRepository.GetByIdAsync(_tagId, Arg.Any<CancellationToken>()).Returns(tag);

        var command = new AddTagToRequestCommand(_requestId, _tagId, _actorId);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(TagErrors.CrossDepartmentNotAllowed.Code);
    }

    [Fact]
    public async Task Handle_ShouldSucceed_AndWriteAuditLog_WhenValidTagIsAdded()
    {
        var request = CreateRequest();
        var user = CreateUser(_deptId);
        var tag = CreateTag();
        var department = new Department("Engineering", Guid.NewGuid(), 100_000m, 500_000m);
        typeof(Department).GetProperty(nameof(Department.Id))!.SetValue(department, _deptId);

        _requestRepository.GetByIdAsync(_requestId, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(_actorId, Arg.Any<CancellationToken>()).Returns(user);
        _tagRepository.GetByIdAsync(_tagId, Arg.Any<CancellationToken>()).Returns(tag);
        _departmentRepository.GetByIdAsync(_deptId, Arg.Any<CancellationToken>()).Returns(department);
        _appSettingRepository.GetByKeyAsync("TagSettings:MaxTagsPerDepartmentOnTicket", Arg.Any<CancellationToken>())
            .Returns((AppSetting?)null); // Uses default limit 5

        var command = new AddTagToRequestCommand(_requestId, _tagId, _actorId);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        request.Tags.Should().HaveCount(1);
        request.Tags[0].TagId.Should().Be(_tagId);

        await _modificationRepository.Received(1).AddAsync(
            Arg.Is<BudgetRequestModification>(m => m.BudgetRequestId == _requestId && m.ModifiedByUserId == _actorId && m.Note!.Contains("Added tag 'Hardware' (Engineering)")),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldRespectCustomMaxLimit_FromAppSetting()
    {
        var request = CreateRequest();
        var user = CreateUser(_deptId);
        var tag1 = CreateTag();

        var customLimitSetting = new AppSetting("TagSettings:MaxTagsPerDepartmentOnTicket", "1", "Max tags per dept");
        _appSettingRepository.GetByKeyAsync("TagSettings:MaxTagsPerDepartmentOnTicket", Arg.Any<CancellationToken>())
            .Returns(customLimitSetting);

        _requestRepository.GetByIdAsync(_requestId, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(_actorId, Arg.Any<CancellationToken>()).Returns(user);
        _tagRepository.GetByIdAsync(_tagId, Arg.Any<CancellationToken>()).Returns(tag1);

        // Add 1st tag
        var cmd1 = new AddTagToRequestCommand(_requestId, _tagId, _actorId);
        var res1 = await _handler.Handle(cmd1, CancellationToken.None);
        res1.IsSuccess.Should().BeTrue();

        // 2nd tag should be blocked by limit 1
        var tag2Id = Guid.NewGuid();
        var tag2 = new Tag(_deptId, "Tag2", _actorId);
        typeof(Tag).GetProperty(nameof(Tag.Id))!.SetValue(tag2, tag2Id);
        _tagRepository.GetByIdAsync(tag2Id, Arg.Any<CancellationToken>()).Returns(tag2);

        var cmd2 = new AddTagToRequestCommand(_requestId, tag2Id, _actorId);
        var res2 = await _handler.Handle(cmd2, CancellationToken.None);

        res2.IsFailure.Should().BeTrue();
        res2.Error.Code.Should().Be(TagErrors.MaxDepartmentTagsReached(1).Code);
    }

    [Fact]
    public async Task Handle_ShouldReturnForbidden_WhenActorRoleIsAccounting()
    {
        var request = CreateRequest();
        var user = CreateUser(_deptId, role: UserRole.Accounting);
        _requestRepository.GetByIdAsync(_requestId, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(_actorId, Arg.Any<CancellationToken>()).Returns(user);

        var command = new AddTagToRequestCommand(_requestId, _tagId, _actorId);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(TagErrors.CannotAttachTag.Code);
    }

    [Fact]
    public async Task Handle_ShouldReturnForbidden_WhenActorRoleIsSupport()
    {
        var request = CreateRequest();
        var user = CreateUser(_deptId, role: UserRole.Support);
        _requestRepository.GetByIdAsync(_requestId, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(_actorId, Arg.Any<CancellationToken>()).Returns(user);

        var command = new AddTagToRequestCommand(_requestId, _tagId, _actorId);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(TagErrors.CannotAttachTag.Code);
    }

    [Fact]
    public async Task Handle_ShouldReturnForbidden_WhenActorIsNotTicketStakeholder()
    {
        var otherRequesterId = Guid.NewGuid();
        var requesterDeptId = Guid.NewGuid();
        var request = BudgetRequest.CreateDraft(
            otherRequesterId,
            "Other Requester",
            BudgetRequestType.Standard,
            Guid.NewGuid(),
            "Head",
            DateTime.UtcNow.AddDays(1),
            100_000m,
            "MMK",
            "Hardware",
            "Withdrawer",
            "Title",
            requesterDeptId,
            false,
            null);
        typeof(BudgetRequest).GetProperty(nameof(BudgetRequest.Id))!.SetValue(request, _requestId);

        var user = CreateUser(_deptId, role: UserRole.Employee); // Not requester, not requester's dept, not finance/mgmt
        _requestRepository.GetByIdAsync(_requestId, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(_actorId, Arg.Any<CancellationToken>()).Returns(user);
        _appSettingRepository.GetByKeyAsync(TagVisibilityConfig.SettingKey, Arg.Any<CancellationToken>())
            .Returns((AppSetting?)null);

        var command = new AddTagToRequestCommand(_requestId, _tagId, _actorId);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(TagErrors.CannotAttachTag.Code);
    }

    [Fact]
    public async Task Handle_ShouldSucceed_WhenActorIsFromFinanceDepartmentConfiguredInTagSettings()
    {
        var otherRequesterId = Guid.NewGuid();
        var requesterDeptId = Guid.NewGuid();
        var request = BudgetRequest.CreateDraft(
            otherRequesterId,
            "Other Requester",
            BudgetRequestType.Standard,
            Guid.NewGuid(),
            "Head",
            DateTime.UtcNow.AddDays(1),
            100_000m,
            "MMK",
            "Hardware",
            "Withdrawer",
            "Title",
            requesterDeptId,
            false,
            null);
        typeof(BudgetRequest).GetProperty(nameof(BudgetRequest.Id))!.SetValue(request, _requestId);

        var financeDeptId = _deptId;
        var user = CreateUser(financeDeptId, role: UserRole.Employee);
        var tag = CreateTag();
        var department = new Department("Finance Dept", Guid.NewGuid(), 100_000m, 500_000m);
        typeof(Department).GetProperty(nameof(Department.Id))!.SetValue(department, financeDeptId);

        var configSetting = new AppSetting(
            TagVisibilityConfig.SettingKey,
            new TagVisibilityConfig { FinanceDepartmentId = financeDeptId, ManagementDepartmentId = Guid.NewGuid() }.ToJson(),
            "Tag settings");

        _requestRepository.GetByIdAsync(_requestId, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(_actorId, Arg.Any<CancellationToken>()).Returns(user);
        _tagRepository.GetByIdAsync(_tagId, Arg.Any<CancellationToken>()).Returns(tag);
        _departmentRepository.GetByIdAsync(financeDeptId, Arg.Any<CancellationToken>()).Returns(department);
        _appSettingRepository.GetByKeyAsync(TagVisibilityConfig.SettingKey, Arg.Any<CancellationToken>()).Returns(configSetting);
        _appSettingRepository.GetByKeyAsync("TagSettings:MaxTagsPerDepartmentOnTicket", Arg.Any<CancellationToken>()).Returns((AppSetting?)null);

        var command = new AddTagToRequestCommand(_requestId, _tagId, _actorId);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        request.Tags.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_ShouldPropagateCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var command = new AddTagToRequestCommand(_requestId, _tagId, _actorId);
        _requestRepository.GetByIdAsync(_requestId, cts.Token).ThrowsAsync(new OperationCanceledException());

        var act = () => _handler.Handle(command, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
