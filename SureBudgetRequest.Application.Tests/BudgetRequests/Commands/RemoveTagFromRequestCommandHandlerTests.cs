using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.BudgetRequests.Commands.RemoveTagFromRequest;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;
using Xunit;

namespace SureBudgetRequest.Application.Tests.BudgetRequests.Commands;

public class RemoveTagFromRequestCommandHandlerTests
{
    private readonly IBudgetRequestRepository _requestRepository = Substitute.For<IBudgetRequestRepository>();
    private readonly ITagRepository _tagRepository = Substitute.For<ITagRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IDepartmentRepository _departmentRepository = Substitute.For<IDepartmentRepository>();
    private readonly IBudgetRequestModificationRepository _modificationRepository = Substitute.For<IBudgetRequestModificationRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private readonly RemoveTagFromRequestCommandHandler _handler;

    private readonly Guid _requestId = Guid.NewGuid();
    private readonly Guid _tagId = Guid.NewGuid();
    private readonly Guid _deptId = Guid.NewGuid();
    private readonly Guid _actorId = Guid.NewGuid();

    public RemoveTagFromRequestCommandHandlerTests()
    {
        _handler = new RemoveTagFromRequestCommandHandler(
            _requestRepository,
            _tagRepository,
            _userRepository,
            _departmentRepository,
            _modificationRepository,
            _unitOfWork);
    }

    private BudgetRequest CreateRequestWithTag(Tag tag, Guid userId, Guid userDeptId)
    {
        var request = BudgetRequest.CreateDraft(
            userId,
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
        request.AddTag(tag, userId, userDeptId);
        return request;
    }

    private Tag CreateTag()
    {
        var tag = new Tag(_deptId, "Hardware", _actorId);
        typeof(Tag).GetProperty(nameof(Tag.Id))!.SetValue(tag, _tagId);
        return tag;
    }

    private User CreateUser(Guid deptId, bool isActive = true)
    {
        var user = new User("actor", "Actor", "actor@test.com", deptId, UserRole.Employee);
        typeof(User).GetProperty(nameof(User.Id))!.SetValue(user, _actorId);
        if (!isActive) user.Deactivate();
        return user;
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenBudgetRequestNotFound()
    {
        _requestRepository.GetByIdAsync(_requestId, Arg.Any<CancellationToken>()).Returns((BudgetRequest?)null);

        var command = new RemoveTagFromRequestCommand(_requestId, _tagId, _actorId);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(BudgetRequestErrors.NotFound(_requestId).Code);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenActorNotFoundOrInactive()
    {
        var tag = CreateTag();
        var request = CreateRequestWithTag(tag, _actorId, _deptId);

        _requestRepository.GetByIdAsync(_requestId, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(_actorId, Arg.Any<CancellationToken>()).Returns((User?)null);

        var command = new RemoveTagFromRequestCommand(_requestId, _tagId, _actorId);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(UserErrors.NotFound(_actorId).Code);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenTagNotFound()
    {
        var tag = CreateTag();
        var request = CreateRequestWithTag(tag, _actorId, _deptId);
        var user = CreateUser(_deptId);

        _requestRepository.GetByIdAsync(_requestId, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(_actorId, Arg.Any<CancellationToken>()).Returns(user);
        _tagRepository.GetByIdAsync(_tagId, Arg.Any<CancellationToken>()).Returns((Tag?)null);

        var command = new RemoveTagFromRequestCommand(_requestId, _tagId, _actorId);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(TagErrors.NotFound.Code);
    }

    [Fact]
    public async Task Handle_ShouldReturnForbidden_WhenUserBelongsToDifferentDepartmentFromTag()
    {
        var tag = CreateTag();
        var request = CreateRequestWithTag(tag, _actorId, _deptId);
        var otherDeptUser = CreateUser(Guid.NewGuid()); // Different dept

        _requestRepository.GetByIdAsync(_requestId, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(_actorId, Arg.Any<CancellationToken>()).Returns(otherDeptUser);
        _tagRepository.GetByIdAsync(_tagId, Arg.Any<CancellationToken>()).Returns(tag);

        var command = new RemoveTagFromRequestCommand(_requestId, _tagId, _actorId);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(TagErrors.CannotRemoveCrossDepartmentTag.Code);
    }

    [Fact]
    public async Task Handle_ShouldSucceed_AndWriteAuditLog_WhenValidTagIsRemoved()
    {
        var tag = CreateTag();
        var request = CreateRequestWithTag(tag, _actorId, _deptId);
        var user = CreateUser(_deptId);
        var department = new Department("Engineering", Guid.NewGuid(), 100_000m, 500_000m);
        typeof(Department).GetProperty(nameof(Department.Id))!.SetValue(department, _deptId);

        _requestRepository.GetByIdAsync(_requestId, Arg.Any<CancellationToken>()).Returns(request);
        _userRepository.GetByIdAsync(_actorId, Arg.Any<CancellationToken>()).Returns(user);
        _tagRepository.GetByIdAsync(_tagId, Arg.Any<CancellationToken>()).Returns(tag);
        _departmentRepository.GetByIdAsync(_deptId, Arg.Any<CancellationToken>()).Returns(department);

        var command = new RemoveTagFromRequestCommand(_requestId, _tagId, _actorId);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        request.Tags.Should().BeEmpty();

        await _modificationRepository.Received(1).AddAsync(
            Arg.Is<BudgetRequestModification>(m => m.BudgetRequestId == _requestId && m.ModifiedByUserId == _actorId && m.Note!.Contains("Removed tag 'Hardware' (Engineering)")),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldPropagateCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var command = new RemoveTagFromRequestCommand(_requestId, _tagId, _actorId);
        _requestRepository.GetByIdAsync(_requestId, cts.Token).ThrowsAsync(new OperationCanceledException());

        var act = () => _handler.Handle(command, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
