using FluentAssertions;
using NSubstitute;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.BudgetRequests.Queries.SearchBudgetRequests;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;
using Xunit;

namespace SureBudgetRequest.Application.Tests.BudgetRequests.Queries;

public class SearchBudgetRequestsQueryTests
{
    private readonly IBudgetRequestRepository _requestRepository = Substitute.For<IBudgetRequestRepository>();
    private readonly ICoaRepository _coaRepository = Substitute.For<ICoaRepository>();
    private readonly IWithdrawMethodRepository _withdrawMethodRepository = Substitute.For<IWithdrawMethodRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();

    private readonly SearchBudgetRequestsQueryHandler _handler;

    private readonly Guid _deptA = Guid.NewGuid();
    private readonly Guid _deptB = Guid.NewGuid();
    private readonly Guid _actorId = Guid.NewGuid();

    public SearchBudgetRequestsQueryTests()
    {
        _handler = new SearchBudgetRequestsQueryHandler(
            _requestRepository,
            _coaRepository,
            _withdrawMethodRepository,
            _userRepository);
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
        typeof(BudgetRequest).GetProperty(nameof(BudgetRequest.Id))!.SetValue(request, Guid.NewGuid());
        return request;
    }

    private Tag CreateTag(Guid deptId, string name)
    {
        var tag = new Tag(deptId, name, _actorId);
        typeof(Tag).GetProperty(nameof(Tag.Id))!.SetValue(tag, Guid.NewGuid());
        return tag;
    }

    [Fact]
    public async Task Handle_DepartmentIsolatedTags_NonAdminViewerOnlySeesOwnDepartmentTags()
    {
        // Arrange
        var viewerId = Guid.NewGuid();
        var viewer = new User("userA", "User A", "userA@test.com", _deptA, UserRole.Employee);
        typeof(User).GetProperty(nameof(User.Id))!.SetValue(viewer, viewerId);
        _userRepository.GetByIdAsync(viewerId, Arg.Any<CancellationToken>()).Returns(viewer);

        var request = CreateRequest();

        var tagA = CreateTag(_deptA, "DeptA-Tag");
        var tagB = CreateTag(_deptB, "DeptB-Tag");

        request.AddTag(tagA, _actorId, _deptA);
        request.AddTag(tagB, _actorId, _deptB);

        _requestRepository.SearchAsync(
            Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<RequestStatus?>(), Arg.Any<IReadOnlyCollection<RequestStatus>?>(),
            Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<Guid?>(), Arg.Any<string?>(), Arg.Any<Guid?>(),
            Arg.Any<bool?>(), Arg.Any<IReadOnlyCollection<BudgetRequestType>?>(), Arg.Any<decimal?>(), Arg.Any<decimal?>(),
            Arg.Any<bool?>(), Arg.Any<string?>(), Arg.Any<bool?>(), Arg.Any<BudgetRequestSortBy>(), Arg.Any<bool>(),
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns((new List<BudgetRequest> { request }, 1));

        var query = new SearchBudgetRequestsQuery(ViewerUserId: viewerId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(1);
        var item = result.Value.Items[0];
        item.Tags.Should().NotBeNull();
        item.Tags!.Should().HaveCount(1);
        item.Tags![0].Name.Should().Be("DeptA-Tag");
        item.Tags[0].DepartmentId.Should().Be(_deptA);
    }

    [Fact]
    public async Task Handle_AdminViewer_SeesAllActiveTagsAcrossDepartments()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var admin = new User("admin", "Admin", "admin@test.com", Guid.Empty, UserRole.Admin);
        typeof(User).GetProperty(nameof(User.Id))!.SetValue(admin, adminId);
        _userRepository.GetByIdAsync(adminId, Arg.Any<CancellationToken>()).Returns(admin);

        var request = CreateRequest();

        var tagA = CreateTag(_deptA, "DeptA-Tag");
        var tagB = CreateTag(_deptB, "DeptB-Tag");

        request.AddTag(tagA, _actorId, _deptA);
        request.AddTag(tagB, _actorId, _deptB);

        _requestRepository.SearchAsync(
            Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<RequestStatus?>(), Arg.Any<IReadOnlyCollection<RequestStatus>?>(),
            Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<Guid?>(), Arg.Any<string?>(), Arg.Any<Guid?>(),
            Arg.Any<bool?>(), Arg.Any<IReadOnlyCollection<BudgetRequestType>?>(), Arg.Any<decimal?>(), Arg.Any<decimal?>(),
            Arg.Any<bool?>(), Arg.Any<string?>(), Arg.Any<bool?>(), Arg.Any<BudgetRequestSortBy>(), Arg.Any<bool>(),
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns((new List<BudgetRequest> { request }, 1));

        var query = new SearchBudgetRequestsQuery(ViewerUserId: adminId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(1);
        var item = result.Value.Items[0];
        item.Tags.Should().NotBeNull();
        item.Tags!.Should().HaveCount(2);
        item.Tags!.Select(t => t.Name).Should().Contain(new[] { "DeptA-Tag", "DeptB-Tag" });
    }
}
