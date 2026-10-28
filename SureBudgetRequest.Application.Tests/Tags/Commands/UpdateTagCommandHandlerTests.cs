using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.Tags.Commands.UpdateTag;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;
using Xunit;

namespace SureBudgetRequest.Application.Tests.Tags.Commands;

public class UpdateTagCommandHandlerTests
{
    private readonly ITagRepository _tagRepository = Substitute.For<ITagRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private readonly UpdateTagCommandHandler _handler;

    private readonly Guid _tagId = Guid.NewGuid();
    private readonly Guid _deptId = Guid.NewGuid();
    private readonly Guid _deptHeadId = Guid.NewGuid();
    private readonly Guid _creatorId = Guid.NewGuid();

    public UpdateTagCommandHandlerTests()
    {
        _handler = new UpdateTagCommandHandler(
            _tagRepository,
            _userRepository,
            _unitOfWork);
    }

    private Tag CreateTag(string name = "OldName", string? color = "#3B82F6")
    {
        var tag = new Tag(_deptId, name, _creatorId, color);
        typeof(Tag).GetProperty(nameof(Tag.Id))!.SetValue(tag, _tagId);
        return tag;
    }

    private User CreateUser(Guid deptId, UserRole role)
    {
        var user = new User("user1", "User One", "user1@test.com", deptId, role);
        typeof(User).GetProperty(nameof(User.Id))!.SetValue(user, _deptHeadId);
        return user;
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenTagNotFound()
    {
        var command = new UpdateTagCommand(_tagId, "NewName", null, _deptHeadId);
        _tagRepository.GetByIdAsync(_tagId, Arg.Any<CancellationToken>()).Returns((Tag?)null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(TagErrors.NotFound.Code);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenActorNotFoundOrInactive()
    {
        var tag = CreateTag();
        _tagRepository.GetByIdAsync(_tagId, Arg.Any<CancellationToken>()).Returns(tag);
        _userRepository.GetByIdAsync(_deptHeadId, Arg.Any<CancellationToken>()).Returns((User?)null);

        var command = new UpdateTagCommand(_tagId, "NewName", null, _deptHeadId);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(UserErrors.NotFound(_deptHeadId).Code);
    }

    [Fact]
    public async Task Handle_ShouldSucceed_WhenRegularEmployeeUpdatesOwnDepartmentTag()
    {
        var tag = CreateTag();
        var employee = CreateUser(_deptId, UserRole.Employee);
        _tagRepository.GetByIdAsync(_tagId, Arg.Any<CancellationToken>()).Returns(tag);
        _userRepository.GetByIdAsync(_deptHeadId, Arg.Any<CancellationToken>()).Returns(employee);
        _tagRepository.GetByNameAndDepartmentAsync("NewName", _deptId, Arg.Any<CancellationToken>())
            .Returns((Tag?)null);

        var command = new UpdateTagCommand(_tagId, "NewName", "#123456", _deptHeadId);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        tag.Name.Should().Be("NewName");
        tag.ColorHex.Should().Be("#123456");
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnForbidden_WhenActorIsDeptHeadOfDifferentDepartment()
    {
        var tag = CreateTag();
        var otherDeptHead = CreateUser(Guid.NewGuid(), UserRole.DepartmentHead); // Different dept
        _tagRepository.GetByIdAsync(_tagId, Arg.Any<CancellationToken>()).Returns(tag);
        _userRepository.GetByIdAsync(_deptHeadId, Arg.Any<CancellationToken>()).Returns(otherDeptHead);

        var command = new UpdateTagCommand(_tagId, "NewName", null, _deptHeadId);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tag.UnauthorizedUpdate");
    }

    [Fact]
    public async Task Handle_ShouldReturnConflict_WhenNewNameConflictsWithAnotherTagInSameDepartment()
    {
        var tag = CreateTag("Original");
        var deptHead = CreateUser(_deptId, UserRole.DepartmentHead);
        _tagRepository.GetByIdAsync(_tagId, Arg.Any<CancellationToken>()).Returns(tag);
        _userRepository.GetByIdAsync(_deptHeadId, Arg.Any<CancellationToken>()).Returns(deptHead);

        var conflictingTag = new Tag(_deptId, "ExistingTag", _creatorId);
        typeof(Tag).GetProperty(nameof(Tag.Id))!.SetValue(conflictingTag, Guid.NewGuid());
        _tagRepository.GetByNameAndDepartmentAsync("ExistingTag", _deptId, Arg.Any<CancellationToken>())
            .Returns(conflictingTag);

        var command = new UpdateTagCommand(_tagId, "ExistingTag", null, _deptHeadId);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(TagErrors.AlreadyExists("ExistingTag").Code);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenDomainValidationFails()
    {
        var tag = CreateTag("Original");
        var deptHead = CreateUser(_deptId, UserRole.DepartmentHead);
        _tagRepository.GetByIdAsync(_tagId, Arg.Any<CancellationToken>()).Returns(tag);
        _userRepository.GetByIdAsync(_deptHeadId, Arg.Any<CancellationToken>()).Returns(deptHead);

        // Empty name should fail domain validation
        var command = new UpdateTagCommand(_tagId, "   ", null, _deptHeadId);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("Tag name is required");
    }

    [Fact]
    public async Task Handle_ShouldSucceed_WhenDeptHeadUpdatesTheirDepartmentTag()
    {
        var tag = CreateTag("Original", "#FFF");
        var deptHead = CreateUser(_deptId, UserRole.DepartmentHead);
        _tagRepository.GetByIdAsync(_tagId, Arg.Any<CancellationToken>()).Returns(tag);
        _userRepository.GetByIdAsync(_deptHeadId, Arg.Any<CancellationToken>()).Returns(deptHead);

        var command = new UpdateTagCommand(_tagId, "UpdatedName", "#10B981", _deptHeadId);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        tag.Name.Should().Be("UpdatedName");
        tag.ColorHex.Should().Be("#10B981");

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldSucceed_WhenAdminUpdatesTagFromAnyDepartment()
    {
        var tag = CreateTag("Original");
        var admin = CreateUser(Guid.NewGuid(), UserRole.Admin); // Admin can be in any department
        _tagRepository.GetByIdAsync(_tagId, Arg.Any<CancellationToken>()).Returns(tag);
        _userRepository.GetByIdAsync(_deptHeadId, Arg.Any<CancellationToken>()).Returns(admin);

        var command = new UpdateTagCommand(_tagId, "AdminUpdated", "#EF4444", _deptHeadId);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        tag.Name.Should().Be("AdminUpdated");
        tag.ColorHex.Should().Be("#EF4444");

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldPropagateCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var command = new UpdateTagCommand(_tagId, "Name", null, _deptHeadId);
        _tagRepository.GetByIdAsync(_tagId, cts.Token).ThrowsAsync(new OperationCanceledException());

        var act = () => _handler.Handle(command, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
