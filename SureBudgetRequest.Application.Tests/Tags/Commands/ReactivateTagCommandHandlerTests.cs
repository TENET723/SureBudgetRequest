using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.Tags.Commands.ReactivateTag;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;
using Xunit;

namespace SureBudgetRequest.Application.Tests.Tags.Commands;

public class ReactivateTagCommandHandlerTests
{
    private readonly ITagRepository _tagRepository = Substitute.For<ITagRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private readonly ReactivateTagCommandHandler _handler;

    private readonly Guid _tagId = Guid.NewGuid();
    private readonly Guid _deptId = Guid.NewGuid();
    private readonly Guid _deptHeadId = Guid.NewGuid();

    public ReactivateTagCommandHandlerTests()
    {
        _handler = new ReactivateTagCommandHandler(
            _tagRepository,
            _userRepository,
            _unitOfWork);
    }

    private Tag CreateTag()
    {
        var tag = new Tag(_deptId, "Tag", Guid.NewGuid());
        typeof(Tag).GetProperty(nameof(Tag.Id))!.SetValue(tag, _tagId);
        tag.Deactivate();
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
        var command = new ReactivateTagCommand(_tagId, _deptHeadId);
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

        var command = new ReactivateTagCommand(_tagId, _deptHeadId);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(UserErrors.NotFound(_deptHeadId).Code);
    }

    [Fact]
    public async Task Handle_ShouldReturnForbidden_WhenActorIsNotDeptHeadOrAdmin()
    {
        var tag = CreateTag();
        var employee = CreateUser(_deptId, UserRole.Employee);
        _tagRepository.GetByIdAsync(_tagId, Arg.Any<CancellationToken>()).Returns(tag);
        _userRepository.GetByIdAsync(_deptHeadId, Arg.Any<CancellationToken>()).Returns(employee);

        var command = new ReactivateTagCommand(_tagId, _deptHeadId);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tag.UnauthorizedUpdate");
    }

    [Fact]
    public async Task Handle_ShouldSucceed_WhenDeptHeadReactivatesTag()
    {
        var tag = CreateTag();
        var deptHead = CreateUser(_deptId, UserRole.DepartmentHead);
        _tagRepository.GetByIdAsync(_tagId, Arg.Any<CancellationToken>()).Returns(tag);
        _userRepository.GetByIdAsync(_deptHeadId, Arg.Any<CancellationToken>()).Returns(deptHead);

        var command = new ReactivateTagCommand(_tagId, _deptHeadId);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        tag.IsActive.Should().BeTrue();
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldPropagateCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var command = new ReactivateTagCommand(_tagId, _deptHeadId);
        _tagRepository.GetByIdAsync(_tagId, cts.Token).ThrowsAsync(new OperationCanceledException());

        var act = () => _handler.Handle(command, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
