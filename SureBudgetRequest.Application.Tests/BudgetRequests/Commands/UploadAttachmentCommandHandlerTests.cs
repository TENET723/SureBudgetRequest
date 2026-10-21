using System.Text;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.Abstractions.Services;
using SureBudgetRequest.Application.BudgetRequests.Commands.UploadAttachment;
using SureBudgetRequest.Application.BudgetRequests.Common;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;
using Xunit;

namespace SureBudgetRequest.Application.Tests.BudgetRequests.Commands;

public class UploadAttachmentCommandHandlerTests
{
    private readonly IBudgetRequestRepository _repository = Substitute.For<IBudgetRequestRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IFileStorage _fileStorage = Substitute.For<IFileStorage>();

    private readonly UploadAttachmentCommandHandler _handler;

    private readonly Guid _requestId = Guid.NewGuid();
    private readonly Guid _requesterId = Guid.NewGuid();
    private readonly Guid _deptHeadId = Guid.NewGuid();
    private readonly Guid _withdrawMethodId = Guid.NewGuid();

    public UploadAttachmentCommandHandlerTests()
    {
        _handler = new UploadAttachmentCommandHandler(
            _repository,
            _unitOfWork,
            _fileStorage);
    }

    private BudgetRequest CreateDraftRequest()
    {
        var request = BudgetRequest.CreateDraft(
            _requesterId,
            "Alice Requester",
            BudgetRequestType.Standard,
            _deptHeadId,
            "Bob Head",
            DateTime.UtcNow.AddDays(1),
            100_000m,
            "MMK",
            "Original reasons",
            "U Ba",
            "Officer",
            _withdrawMethodId,
            false,
            null);

        typeof(BudgetRequest).GetProperty(nameof(BudgetRequest.Id))!.SetValue(request, _requestId);
        return request;
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_ShouldReturnFailure_WhenFileNameIsNullOrWhiteSpace(string? fileName)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("test"));
        var command = new UploadAttachmentCommand(
            _requestId,
            _requesterId,
            fileName!,
            "application/pdf",
            100,
            stream);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Be("File name is required.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-500)]
    public async Task Handle_ShouldReturnFailure_WhenSizeBytesIsZeroOrNegative(long sizeBytes)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("test"));
        var command = new UploadAttachmentCommand(
            _requestId,
            _requesterId,
            "doc.pdf",
            "application/pdf",
            sizeBytes,
            stream);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Be("File size must be greater than zero.");
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenSizeBytesExceedsMaxLimit()
    {
        using var stream = new MemoryStream();
        var command = new UploadAttachmentCommand(
            _requestId,
            _requesterId,
            "huge.pdf",
            "application/pdf",
            AttachmentConstraints.MaxBytes + 1,
            stream);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("File exceeds the");
    }

    [Theory]
    [InlineData("application/x-msdownload")]
    [InlineData("application/octet-stream")]
    [InlineData("text/html")]
    [InlineData("application/javascript")]
    public async Task Handle_ShouldReturnFailure_WhenContentTypeIsNotAllowed(string invalidContentType)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("content"));
        var command = new UploadAttachmentCommand(
            _requestId,
            _requesterId,
            "script.exe",
            invalidContentType,
            100,
            stream);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("is not allowed");
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenBudgetRequestNotFound()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("content"));
        var command = new UploadAttachmentCommand(
            _requestId,
            _requesterId,
            "invoice.pdf",
            "application/pdf",
            100,
            stream);

        _repository.GetByIdAsync(_requestId, Arg.Any<CancellationToken>()).Returns((BudgetRequest?)null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Be("Budget request not found.");
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenFileStorageSaveAsyncThrows()
    {
        var request = CreateDraftRequest();
        _repository.GetByIdAsync(_requestId, Arg.Any<CancellationToken>()).Returns(request);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("content"));
        var command = new UploadAttachmentCommand(
            _requestId,
            _requesterId,
            "invoice.pdf",
            "application/pdf",
            100,
            stream);

        _fileStorage.SaveAsync(_requestId.ToString(), "invoice.pdf", stream, Arg.Any<CancellationToken>())
            .ThrowsAsync(new IOException("Disk quota exceeded"));

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("Failed to store the file: Disk quota exceeded");
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenFileStorageMoveAsyncThrows_ForPreUploadedPath()
    {
        var request = CreateDraftRequest();
        _repository.GetByIdAsync(_requestId, Arg.Any<CancellationToken>()).Returns(request);

        using var stream = new MemoryStream();
        var command = new UploadAttachmentCommand(
            _requestId,
            _requesterId,
            "invoice.pdf",
            "application/pdf",
            100,
            stream,
            PreUploadedPath: "temp/staging123.tmp");

        _fileStorage.MoveAsync("temp/staging123.tmp", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new FileNotFoundException("Staging file missing"));

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("Failed to store the file: Staging file missing");
    }

    [Fact]
    public async Task Handle_ShouldReturnFailureAndCleanupStorage_WhenDomainAddAttachmentFails()
    {
        var request = CreateDraftRequest();
        _repository.GetByIdAsync(_requestId, Arg.Any<CancellationToken>()).Returns(request);

        // Uploading by a non-requester user should fail domain validation
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("content"));
        var nonRequester = Guid.NewGuid();
        var command = new UploadAttachmentCommand(
            _requestId,
            nonRequester,
            "invoice.pdf",
            "application/pdf",
            100,
            stream);

        _fileStorage.SaveAsync(_requestId.ToString(), "invoice.pdf", stream, Arg.Any<CancellationToken>())
            .Returns("storage/saved-invoice.pdf");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Be("Only the requester can add this type of attachment.");

        // Must attempt cleanup of the saved file
        await _fileStorage.Received(1).DeleteAsync("storage/saved-invoice.pdf", CancellationToken.None);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldRethrowAndCleanupStorage_WhenUnitOfWorkThrows()
    {
        var request = CreateDraftRequest();
        _repository.GetByIdAsync(_requestId, Arg.Any<CancellationToken>()).Returns(request);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("content"));
        var command = new UploadAttachmentCommand(
            _requestId,
            _requesterId,
            "invoice.pdf",
            "application/pdf",
            100,
            stream);

        _fileStorage.SaveAsync(_requestId.ToString(), "invoice.pdf", stream, Arg.Any<CancellationToken>())
            .Returns("storage/saved-invoice.pdf");

        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("DB deadlock"));

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("DB deadlock");
        await _fileStorage.Received(1).DeleteAsync("storage/saved-invoice.pdf", CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldSucceed_WhenUploadViaContentStream_ForDraftStatus()
    {
        var request = CreateDraftRequest();
        _repository.GetByIdAsync(_requestId, Arg.Any<CancellationToken>()).Returns(request);

        var generatedAttachmentId = Guid.NewGuid();
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                // Simulate EF Core assigning generated ID to new attachment
                typeof(Attachment).GetProperty(nameof(Attachment.Id))!
                    .SetValue(request.Attachments[^1], generatedAttachmentId);
                return Task.FromResult(1);
            });

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("content"));
        var command = new UploadAttachmentCommand(
            _requestId,
            _requesterId,
            "invoice.pdf",
            "application/pdf",
            100,
            stream);

        _fileStorage.SaveAsync(_requestId.ToString(), "invoice.pdf", stream, Arg.Any<CancellationToken>())
            .Returns("storage/saved-invoice.pdf");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(generatedAttachmentId);

        request.Attachments.Should().HaveCount(1);
        request.Attachments[0].FileName.Should().Be("invoice.pdf");
        request.Attachments[0].StoredPath.Should().Be("storage/saved-invoice.pdf");
        request.Attachments[0].ContentType.Should().Be("application/pdf");
        request.Attachments[0].Category.Should().Be(AttachmentCategory.General);

        await _fileStorage.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("sample.pdf", ".pdf")]
    [InlineData("image.PNG", ".png")]
    [InlineData("photo.jpg", ".jpg")]
    [InlineData("photo.jpeg", ".jpeg")]
    [InlineData("animation.gif", ".gif")]
    [InlineData("art.webp", ".webp")]
    [InlineData("document.doc", ".doc")]
    [InlineData("document.docx", ".docx")]
    [InlineData("sheet.xls", ".xls")]
    [InlineData("sheet.xlsx", ".xlsx")]
    [InlineData("data.txt", ".txt")]
    [InlineData("data.csv", ".csv")]
    public async Task Handle_ShouldSucceed_WhenUploadViaPreUploadedPath_WithRecognizedExtensions(
        string fileName,
        string expectedExtension)
    {
        var request = CreateDraftRequest();
        _repository.GetByIdAsync(_requestId, Arg.Any<CancellationToken>()).Returns(request);

        using var stream = new MemoryStream();
        var command = new UploadAttachmentCommand(
            _requestId,
            _requesterId,
            fileName,
            "application/pdf",
            100,
            stream,
            PreUploadedPath: "temp/staging123.tmp");

        string? capturedDestination = null;
        await _fileStorage.MoveAsync("temp/staging123.tmp", Arg.Do<string>(s => capturedDestination = s), Arg.Any<CancellationToken>());

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        capturedDestination.Should().NotBeNull();
        capturedDestination.Should().EndWith(expectedExtension);
        capturedDestination.Should().StartWith($"{_requestId}/");
    }

    [Fact]
    public async Task Handle_ShouldFallbackToBinExtension_WhenPreUploadedFileNameHasUnrecognizedExtension()
    {
        var request = CreateDraftRequest();
        _repository.GetByIdAsync(_requestId, Arg.Any<CancellationToken>()).Returns(request);

        using var stream = new MemoryStream();
        var command = new UploadAttachmentCommand(
            _requestId,
            _requesterId,
            "archive.unknownext",
            "application/pdf",
            100,
            stream,
            PreUploadedPath: "temp/staging123.tmp");

        string? capturedDestination = null;
        await _fileStorage.MoveAsync("temp/staging123.tmp", Arg.Do<string>(s => capturedDestination = s), Arg.Any<CancellationToken>());

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        capturedDestination.Should().EndWith(".bin");
    }

    [Fact]
    public async Task Handle_ShouldSwallowCleanupException_WhenTryDeleteFromStorageThrows()
    {
        var request = CreateDraftRequest();
        _repository.GetByIdAsync(_requestId, Arg.Any<CancellationToken>()).Returns(request);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("content"));
        var nonRequester = Guid.NewGuid();
        var command = new UploadAttachmentCommand(
            _requestId,
            nonRequester,
            "invoice.pdf",
            "application/pdf",
            100,
            stream);

        _fileStorage.SaveAsync(_requestId.ToString(), "invoice.pdf", stream, Arg.Any<CancellationToken>())
            .Returns("storage/saved-invoice.pdf");

        _fileStorage.DeleteAsync("storage/saved-invoice.pdf", CancellationToken.None)
            .ThrowsAsync(new IOException("Storage unreachable during cleanup"));

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Be("Only the requester can add this type of attachment.");
    }

    [Fact]
    public async Task Handle_ShouldPropagateCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("content"));
        var command = new UploadAttachmentCommand(
            _requestId,
            _requesterId,
            "invoice.pdf",
            "application/pdf",
            100,
            stream);

        _repository.GetByIdAsync(_requestId, cts.Token)
            .ThrowsAsync(new OperationCanceledException());

        var act = () => _handler.Handle(command, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
