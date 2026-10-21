using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.Abstractions.Services;
using SureBudgetRequest.Application.BudgetRequests.Commands.RemoveAttachment;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;
using Xunit;

namespace SureBudgetRequest.Application.Tests.BudgetRequests.Commands;

public class RemoveAttachmentCommandHandlerTests
{
    private readonly IBudgetRequestRepository _repository = Substitute.For<IBudgetRequestRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IFileStorage _fileStorage = Substitute.For<IFileStorage>();
    private readonly ILogger<RemoveAttachmentCommandHandler> _logger = Substitute.For<ILogger<RemoveAttachmentCommandHandler>>();
    private readonly RemoveAttachmentCommandHandler _handler;

    public RemoveAttachmentCommandHandlerTests()
    {
        _handler = new RemoveAttachmentCommandHandler(
            _repository,
            _unitOfWork,
            _fileStorage,
            _logger);
    }

    private static BudgetRequest CreateDraftWithAttachment(Guid requesterId, out Guid attachmentId, string storedPath = "files/test.pdf")
    {
        var request = BudgetRequest.CreateDraft(
            requesterId, "Alice", BudgetRequestType.Standard, Guid.NewGuid(), "Bob Head",
            DateTime.UtcNow.AddDays(2), 50_000m, "MMK", "Test", "Alice", "Job",
            Guid.NewGuid(), false, null);

        request.AddAttachment("test.pdf", storedPath, "application/pdf", 1024, requesterId, AttachmentCategory.General);

        var att = request.Attachments.First();
        attachmentId = Guid.NewGuid();
        typeof(Attachment).GetProperty(nameof(Attachment.Id))!.SetValue(att, attachmentId);

        return request;
    }

    [Fact]
    public async Task Handle_WhenBudgetRequestNotFound_ReturnsNotFoundError()
    {
        // Arrange
        var reqId = Guid.NewGuid();
        _repository.GetByIdAsync(reqId, Arg.Any<CancellationToken>()).Returns((BudgetRequest?)null);

        var command = new RemoveAttachmentCommand(reqId, Guid.NewGuid(), Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BudgetRequestErrors.NotFound(reqId));

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenUserIsNotRequester_ReturnsDomainFailureWithoutDeletingFileOrSaving()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var request = CreateDraftWithAttachment(requesterId, out var attId, "files/test.pdf");

        _repository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);

        var command = new RemoveAttachmentCommand(request.Id, attId, otherUserId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("Only the requester can remove attachments");

        await _fileStorage.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenAttachmentNotFoundOnRequest_ReturnsDomainFailure()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var request = CreateDraftWithAttachment(requesterId, out _, "files/test.pdf");
        var missingAttId = Guid.NewGuid();

        _repository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);

        var command = new RemoveAttachmentCommand(request.Id, missingAttId, requesterId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("Attachment not found on this request");

        await _fileStorage.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenValid_RemovesAttachmentDeletesFileAndSaves()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var storedPath = "files/test.pdf";
        var request = CreateDraftWithAttachment(requesterId, out var attId, storedPath);

        _repository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);

        var command = new RemoveAttachmentCommand(request.Id, attId, requesterId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.Attachments.Should().BeEmpty();

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _fileStorage.Received(1).DeleteAsync(storedPath, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_WhenFileStorageThrowsException_CatchesExceptionAndStillReturnsSuccess()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var storedPath = "files/orphan.pdf";
        var request = CreateDraftWithAttachment(requesterId, out var attId, storedPath);

        _repository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        _fileStorage.DeleteAsync(storedPath, CancellationToken.None)
            .ThrowsAsync(new InvalidOperationException("Storage unreachable"));

        var command = new RemoveAttachmentCommand(request.Id, attId, requesterId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert: Best-effort delete logs warning but still returns success!
        result.IsSuccess.Should().BeTrue();
        request.Attachments.Should().BeEmpty();

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PassesCancellationTokenToDependencies()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var storedPath = "files/test.pdf";
        var request = CreateDraftWithAttachment(requesterId, out var attId, storedPath);

        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        _repository.GetByIdAsync(request.Id, ct).Returns(request);

        var command = new RemoveAttachmentCommand(request.Id, attId, requesterId);

        // Act
        var result = await _handler.Handle(command, ct);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _repository.Received(1).GetByIdAsync(request.Id, ct);
        await _unitOfWork.Received(1).SaveChangesAsync(ct);
    }
}
