using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.Tags.Commands.CreateTag;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;
using Xunit;

namespace SureBudgetRequest.Application.Tests.Tags.Commands;

public class CreateTagCommandHandlerTests
{
    private readonly ITagRepository _tagRepository = Substitute.For<ITagRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private readonly CreateTagCommandHandler _handler;

    private readonly Guid _deptId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public CreateTagCommandHandlerTests()
    {
        _handler = new CreateTagCommandHandler(
            _tagRepository,
            _userRepository,
            _unitOfWork);
    }

    private User CreateActiveUser(Guid deptId, UserRole role = UserRole.Employee)
    {
        var user = new User("alice", "Alice Requester", "alice@test.com", deptId, role);
        typeof(User).GetProperty(nameof(User.Id))!.SetValue(user, _userId);
        return user;
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenUserNotFound()
    {
        var command = new CreateTagCommand(_deptId, "Campaign", _userId);
        _userRepository.GetByIdAsync(_userId, Arg.Any<CancellationToken>()).Returns((User?)null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(UserErrors.NotFound(_userId).Code);
        await _tagRepository.DidNotReceive().AddAsync(Arg.Any<Tag>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenUserIsInactive()
    {
        var user = CreateActiveUser(_deptId);
        user.Deactivate();
        _userRepository.GetByIdAsync(_userId, Arg.Any<CancellationToken>()).Returns(user);

        var command = new CreateTagCommand(_deptId, "Campaign", _userId);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(UserErrors.NotFound(_userId).Code);
    }

    [Fact]
    public async Task Handle_ShouldReturnForbidden_WhenUserBelongsToDifferentDepartment()
    {
        var differentDeptId = Guid.NewGuid();
        var user = CreateActiveUser(differentDeptId); // User in another dept
        _userRepository.GetByIdAsync(_userId, Arg.Any<CancellationToken>()).Returns(user);

        var command = new CreateTagCommand(_deptId, "Campaign", _userId);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(TagErrors.CrossDepartmentNotAllowed.Code);
    }

    [Fact]
    public async Task Handle_ShouldSucceed_WhenAdminCreatesTagForAnyDepartment()
    {
        var differentDeptId = Guid.NewGuid();
        var admin = CreateActiveUser(differentDeptId, UserRole.Admin);
        _userRepository.GetByIdAsync(_userId, Arg.Any<CancellationToken>()).Returns(admin);
        _tagRepository.GetByNameAndDepartmentAsync("AdminTag", _deptId, Arg.Any<CancellationToken>())
            .Returns((Tag?)null);

        var command = new CreateTagCommand(_deptId, "AdminTag", _userId);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _tagRepository.Received(1).AddAsync(
            Arg.Is<Tag>(t => t.DepartmentId == _deptId && t.Name == "AdminTag"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnConflict_WhenTagWithSameNameAlreadyExistsInDepartment()
    {
        var user = CreateActiveUser(_deptId);
        _userRepository.GetByIdAsync(_userId, Arg.Any<CancellationToken>()).Returns(user);

        var existingTag = new Tag(_deptId, "Campaign", _userId);
        _tagRepository.GetByNameAndDepartmentAsync("Campaign", _deptId, Arg.Any<CancellationToken>())
            .Returns(existingTag);

        var command = new CreateTagCommand(_deptId, "Campaign", _userId);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(TagErrors.AlreadyExists("Campaign").Code);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenDomainValidationFails()
    {
        var user = CreateActiveUser(_deptId);
        _userRepository.GetByIdAsync(_userId, Arg.Any<CancellationToken>()).Returns(user);
        _tagRepository.GetByNameAndDepartmentAsync("Campaign", _deptId, Arg.Any<CancellationToken>())
            .Returns((Tag?)null);

        // Invalid color code fails domain normalization
        var command = new CreateTagCommand(_deptId, "Campaign", _userId, ColorHex: "not-a-color");
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("valid hex color code");
    }

    [Fact]
    public async Task Handle_ShouldSucceed_WhenValidTagIsCreated()
    {
        var user = CreateActiveUser(_deptId);
        _userRepository.GetByIdAsync(_userId, Arg.Any<CancellationToken>()).Returns(user);
        _tagRepository.GetByNameAndDepartmentAsync("Campaign-Q4", _deptId, Arg.Any<CancellationToken>())
            .Returns((Tag?)null);

        var command = new CreateTagCommand(_deptId, "  Campaign-Q4  ", _userId, "#3b82f6");
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        await _tagRepository.Received(1).AddAsync(
            Arg.Is<Tag>(t => t.DepartmentId == _deptId && t.Name == "Campaign-Q4" && t.ColorHex == "#3B82F6" && t.CreatedByUserId == _userId),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldPropagateCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var command = new CreateTagCommand(_deptId, "Campaign", _userId);
        _userRepository.GetByIdAsync(_userId, cts.Token).ThrowsAsync(new OperationCanceledException());

        var act = () => _handler.Handle(command, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
