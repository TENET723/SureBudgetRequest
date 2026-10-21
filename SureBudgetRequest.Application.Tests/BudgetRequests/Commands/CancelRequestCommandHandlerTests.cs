using FluentAssertions;
using NSubstitute;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.BudgetRequests.Commands.CancelRequest;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;
using Xunit;

namespace SureBudgetRequest.Application.Tests.BudgetRequests.Commands;

public class CancelRequestCommandHandlerTests
{
    private readonly IBudgetRequestRepository _repository = Substitute.For<IBudgetRequestRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly CancelRequestCommandHandler _handler;

    public CancelRequestCommandHandlerTests()
    {
        _handler = new CancelRequestCommandHandler(_repository, _unitOfWork);
    }

    private static BudgetRequest CreateDraftRequest(Guid requesterId)
    {
        return BudgetRequest.CreateDraft(
            requesterId, "Alice", BudgetRequestType.Standard, Guid.NewGuid(), "Bob Head",
            DateTime.UtcNow.AddHours(6.5).Date.AddDays(2),
            50_000m, "MMK", "Test title", "Alice", "Job", Guid.NewGuid(),
            false, null);
    }

    [Fact]
    public async Task Handle_WhenBudgetRequestNotFound_ReturnsNotFoundError()
    {
        // Arrange
        var reqId = Guid.NewGuid();
        _repository.GetByIdAsync(reqId, Arg.Any<CancellationToken>()).Returns((BudgetRequest?)null);

        var command = new CancelRequestCommand(reqId, Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BudgetRequestErrors.NotFound(reqId));

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenUserIsNotRequester_ReturnsDomainFailureWithoutSaving()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var request = CreateDraftRequest(requesterId);

        _repository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);

        var command = new CancelRequestCommand(request.Id, otherUserId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("Only the requester can cancel");

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenRequestAlreadyCancelled_ReturnsDomainFailureWithoutSaving()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var request = CreateDraftRequest(requesterId);
        request.Cancel(requesterId); // already cancelled

        _repository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);

        var command = new CancelRequestCommand(request.Id, requesterId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("Cannot cancel a request in status 'Cancelled'");

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenValid_CancelsRequestSavesChangesAndReturnsSuccess()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var request = CreateDraftRequest(requesterId);

        _repository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);

        var command = new CancelRequestCommand(request.Id, requesterId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.Status.Should().Be(RequestStatus.Cancelled);

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PassesCancellationTokenToDependencies()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var request = CreateDraftRequest(requesterId);
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        _repository.GetByIdAsync(request.Id, ct).Returns(request);

        var command = new CancelRequestCommand(request.Id, requesterId);

        // Act
        var result = await _handler.Handle(command, ct);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _repository.Received(1).GetByIdAsync(request.Id, ct);
        await _unitOfWork.Received(1).SaveChangesAsync(ct);
    }
}
