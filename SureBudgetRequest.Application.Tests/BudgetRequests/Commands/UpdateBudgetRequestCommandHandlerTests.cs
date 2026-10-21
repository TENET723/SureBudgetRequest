using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.Abstractions.Services;
using SureBudgetRequest.Application.BudgetRequests.Commands.UpdateBudgetRequest;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;
using Xunit;

namespace SureBudgetRequest.Application.Tests.BudgetRequests.Commands;

public class UpdateBudgetRequestCommandHandlerTests
{
    private readonly IBudgetRequestRepository _repository = Substitute.For<IBudgetRequestRepository>();
    private readonly ICurrencyRepository _currencyRepository = Substitute.For<ICurrencyRepository>();
    private readonly IWithdrawMethodRepository _withdrawMethodRepository = Substitute.For<IWithdrawMethodRepository>();
    private readonly IBudgetCategoryRepository _budgetCategoryRepository = Substitute.For<IBudgetCategoryRepository>();
    private readonly IBudgetRequestModificationRepository _modificationRepository = Substitute.For<IBudgetRequestModificationRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IFileStorage _fileStorage = Substitute.For<IFileStorage>();
    private readonly ILogger<UpdateBudgetRequestCommandHandler> _logger = Substitute.For<ILogger<UpdateBudgetRequestCommandHandler>>();

    private readonly UpdateBudgetRequestCommandHandler _handler;

    private readonly Guid _requestId = Guid.NewGuid();
    private readonly Guid _requesterId = Guid.NewGuid();
    private readonly Guid _deptHeadId = Guid.NewGuid();
    private readonly Guid _departmentId = Guid.NewGuid();
    private readonly Guid _withdrawMethodId = Guid.NewGuid();
    private readonly Guid _budgetCategoryId = Guid.NewGuid();

    public UpdateBudgetRequestCommandHandlerTests()
    {
        _handler = new UpdateBudgetRequestCommandHandler(
            _repository,
            _currencyRepository,
            _withdrawMethodRepository,
            _budgetCategoryRepository,
            _modificationRepository,
            _unitOfWork,
            _fileStorage,
            _logger);
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

    private Currency CreateActiveCurrency(string code = "MMK")
    {
        return new Currency(code, code == "MMK" ? "Myanmar Kyat" : "US Dollar", 1.0m);
    }

    private WithdrawMethod CreateWithdrawMethod(bool requiresAttachment = false, bool isActive = true)
    {
        var wm = new WithdrawMethod("Cash", requiresAttachment);
        typeof(WithdrawMethod).GetProperty(nameof(WithdrawMethod.Id))!.SetValue(wm, _withdrawMethodId);
        if (!isActive)
        {
            wm.Deactivate();
        }
        return wm;
    }

    private BudgetCategory CreateBudgetCategory(bool isActive = true)
    {
        var cat = new BudgetCategory("IT Equipment");
        typeof(BudgetCategory).GetProperty(nameof(BudgetCategory.Id))!.SetValue(cat, _budgetCategoryId);
        if (!isActive)
        {
            cat.Deactivate();
        }
        return cat;
    }

    private void SubmitRequest(BudgetRequest request)
    {
        request.Submit(
            _departmentId,
            500_000m,
            1_000_000m,
            0m,
            DateTime.UtcNow.Date,
            DateTime.UtcNow.Date.AddDays(30),
            1.0m,
            _deptHeadId,
            "Bob Head",
            "Alice Requester",
            false,
            1);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenWithdrawMethodIdIsEmpty()
    {
        var command = new UpdateBudgetRequestCommand(
            _requestId,
            _requesterId,
            BudgetRequestType.Standard,
            DateTime.UtcNow.AddDays(1),
            50_000m,
            "MMK",
            "Updated reasons",
            "Withdrawer",
            "Manager",
            Guid.Empty,
            false,
            null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(WithdrawMethodErrors.Required.Code);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenRequestNotFound()
    {
        var command = new UpdateBudgetRequestCommand(
            _requestId,
            _requesterId,
            BudgetRequestType.Standard,
            DateTime.UtcNow.AddDays(1),
            50_000m,
            "MMK",
            "Updated reasons",
            "Withdrawer",
            "Manager",
            _withdrawMethodId,
            false,
            null);

        _repository.GetByIdAsync(_requestId, Arg.Any<CancellationToken>()).Returns((BudgetRequest?)null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(BudgetRequestErrors.NotFound(_requestId).Code);
    }

    [Fact]
    public async Task Handle_ShouldReturnForbidden_WhenActorIsNotRequester_ForDraftStatus()
    {
        var request = CreateDraftRequest();
        _repository.GetByIdAsync(_requestId, Arg.Any<CancellationToken>()).Returns(request);

        var command = new UpdateBudgetRequestCommand(
            _requestId,
            Guid.NewGuid(), // Different actor
            BudgetRequestType.Standard,
            DateTime.UtcNow.AddDays(1),
            50_000m,
            "MMK",
            "Updated reasons",
            "Withdrawer",
            "Manager",
            _withdrawMethodId,
            false,
            null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("BudgetRequest.UpdateUnauthorized");
    }

    [Fact]
    public async Task Handle_ShouldReturnForbidden_WhenActorIsNotRequester_ForSentBackStatus()
    {
        var request = CreateDraftRequest();
        SubmitRequest(request);
        request.SendBack(_deptHeadId, "Needs revision");
        _repository.GetByIdAsync(_requestId, Arg.Any<CancellationToken>()).Returns(request);

        var command = new UpdateBudgetRequestCommand(
            _requestId,
            Guid.NewGuid(), // Not requester
            BudgetRequestType.Standard,
            DateTime.UtcNow.AddDays(1),
            50_000m,
            "MMK",
            "Updated reasons",
            "Withdrawer",
            "Manager",
            _withdrawMethodId,
            false,
            null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("BudgetRequest.UpdateUnauthorized");
    }

    [Fact]
    public async Task Handle_ShouldReturnForbidden_WhenActorIsNotDeptHead_ForPendingDeptHeadStatus()
    {
        var request = CreateDraftRequest();
        SubmitRequest(request);
        _repository.GetByIdAsync(_requestId, Arg.Any<CancellationToken>()).Returns(request);

        var command = new UpdateBudgetRequestCommand(
            _requestId,
            _requesterId, // Requester cannot update during PendingDeptHead
            BudgetRequestType.Standard,
            DateTime.UtcNow.AddDays(1),
            50_000m,
            "MMK",
            "Updated reasons",
            "Withdrawer",
            "Manager",
            _withdrawMethodId,
            false,
            null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("BudgetRequest.UpdateUnauthorized");
    }

    [Fact]
    public async Task Handle_ShouldReturnForbidden_WhenStatusIsNotDraftSentBackOrPendingDeptHead()
    {
        var request = CreateDraftRequest();
        SubmitRequest(request);
        request.ApproveBy(_deptHeadId); // Now PendingFinance
        _repository.GetByIdAsync(_requestId, Arg.Any<CancellationToken>()).Returns(request);

        var command = new UpdateBudgetRequestCommand(
            _requestId,
            _deptHeadId,
            BudgetRequestType.Standard,
            DateTime.UtcNow.AddDays(1),
            50_000m,
            "MMK",
            "Updated reasons",
            "Withdrawer",
            "Manager",
            _withdrawMethodId,
            false,
            null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("BudgetRequest.UpdateUnauthorized");
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenCurrencyNotFound()
    {
        var request = CreateDraftRequest();
        _repository.GetByIdAsync(_requestId, Arg.Any<CancellationToken>()).Returns(request);
        _currencyRepository.GetByCodeAsync("USD", Arg.Any<CancellationToken>()).Returns((Currency?)null);

        var command = new UpdateBudgetRequestCommand(
            _requestId,
            _requesterId,
            BudgetRequestType.Standard,
            DateTime.UtcNow.AddDays(1),
            50_000m,
            "USD",
            "Updated reasons",
            "Withdrawer",
            "Manager",
            _withdrawMethodId,
            false,
            null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(CurrencyErrors.NotFound("USD").Code);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenCurrencyInactive()
    {
        var request = CreateDraftRequest();
        _repository.GetByIdAsync(_requestId, Arg.Any<CancellationToken>()).Returns(request);
        var inactiveCurrency = new Currency("USD", "US Dollar", 2100m);
        inactiveCurrency.Deactivate();
        _currencyRepository.GetByCodeAsync("USD", Arg.Any<CancellationToken>()).Returns(inactiveCurrency);

        var command = new UpdateBudgetRequestCommand(
            _requestId,
            _requesterId,
            BudgetRequestType.Standard,
            DateTime.UtcNow.AddDays(1),
            50_000m,
            "USD",
            "Updated reasons",
            "Withdrawer",
            "Manager",
            _withdrawMethodId,
            false,
            null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(CurrencyErrors.Inactive("USD").Code);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenWithdrawMethodNotFound()
    {
        var request = CreateDraftRequest();
        _repository.GetByIdAsync(_requestId, Arg.Any<CancellationToken>()).Returns(request);
        _currencyRepository.GetByCodeAsync("MMK", Arg.Any<CancellationToken>()).Returns(CreateActiveCurrency());
        _withdrawMethodRepository.GetByIdAsync(_withdrawMethodId, Arg.Any<CancellationToken>()).Returns((WithdrawMethod?)null);

        var command = new UpdateBudgetRequestCommand(
            _requestId,
            _requesterId,
            BudgetRequestType.Standard,
            DateTime.UtcNow.AddDays(1),
            50_000m,
            "MMK",
            "Updated reasons",
            "Withdrawer",
            "Manager",
            _withdrawMethodId,
            false,
            null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(WithdrawMethodErrors.NotFound.Code);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenWithdrawMethodInactive()
    {
        var request = CreateDraftRequest();
        _repository.GetByIdAsync(_requestId, Arg.Any<CancellationToken>()).Returns(request);
        _currencyRepository.GetByCodeAsync("MMK", Arg.Any<CancellationToken>()).Returns(CreateActiveCurrency());
        var inactiveWm = CreateWithdrawMethod(isActive: false);
        _withdrawMethodRepository.GetByIdAsync(_withdrawMethodId, Arg.Any<CancellationToken>()).Returns(inactiveWm);

        var command = new UpdateBudgetRequestCommand(
            _requestId,
            _requesterId,
            BudgetRequestType.Standard,
            DateTime.UtcNow.AddDays(1),
            50_000m,
            "MMK",
            "Updated reasons",
            "Withdrawer",
            "Manager",
            _withdrawMethodId,
            false,
            null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(WithdrawMethodErrors.Inactive(inactiveWm.Name).Code);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenBudgetCategoryNotFound()
    {
        var request = CreateDraftRequest();
        _repository.GetByIdAsync(_requestId, Arg.Any<CancellationToken>()).Returns(request);
        _currencyRepository.GetByCodeAsync("MMK", Arg.Any<CancellationToken>()).Returns(CreateActiveCurrency());
        _withdrawMethodRepository.GetByIdAsync(_withdrawMethodId, Arg.Any<CancellationToken>()).Returns(CreateWithdrawMethod());
        _budgetCategoryRepository.GetByIdAsync(_budgetCategoryId, Arg.Any<CancellationToken>()).Returns((BudgetCategory?)null);

        var command = new UpdateBudgetRequestCommand(
            _requestId,
            _requesterId,
            BudgetRequestType.Standard,
            DateTime.UtcNow.AddDays(1),
            50_000m,
            "MMK",
            "Updated reasons",
            "Withdrawer",
            "Manager",
            _withdrawMethodId,
            false,
            null,
            BudgetCategoryId: _budgetCategoryId);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(BudgetCategoryErrors.NotFound.Code);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenBudgetCategoryInactive()
    {
        var request = CreateDraftRequest();
        _repository.GetByIdAsync(_requestId, Arg.Any<CancellationToken>()).Returns(request);
        _currencyRepository.GetByCodeAsync("MMK", Arg.Any<CancellationToken>()).Returns(CreateActiveCurrency());
        _withdrawMethodRepository.GetByIdAsync(_withdrawMethodId, Arg.Any<CancellationToken>()).Returns(CreateWithdrawMethod());
        var inactiveCategory = CreateBudgetCategory(isActive: false);
        _budgetCategoryRepository.GetByIdAsync(_budgetCategoryId, Arg.Any<CancellationToken>()).Returns(inactiveCategory);

        var command = new UpdateBudgetRequestCommand(
            _requestId,
            _requesterId,
            BudgetRequestType.Standard,
            DateTime.UtcNow.AddDays(1),
            50_000m,
            "MMK",
            "Updated reasons",
            "Withdrawer",
            "Manager",
            _withdrawMethodId,
            false,
            null,
            BudgetCategoryId: _budgetCategoryId);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(BudgetCategoryErrors.Inactive(inactiveCategory.Name).Code);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenDomainValidationFails()
    {
        var request = CreateDraftRequest();
        _repository.GetByIdAsync(_requestId, Arg.Any<CancellationToken>()).Returns(request);
        _currencyRepository.GetByCodeAsync("MMK", Arg.Any<CancellationToken>()).Returns(CreateActiveCurrency());
        _withdrawMethodRepository.GetByIdAsync(_withdrawMethodId, Arg.Any<CancellationToken>()).Returns(CreateWithdrawMethod());

        // Zero amount should fail domain validation
        var command = new UpdateBudgetRequestCommand(
            _requestId,
            _requesterId,
            BudgetRequestType.Standard,
            DateTime.UtcNow.AddDays(1),
            0m,
            "MMK",
            "Updated reasons",
            "Withdrawer",
            "Manager",
            _withdrawMethodId,
            false,
            null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("Requested amount must be greater than zero.");
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldSucceed_WhenValidDraftUpdatedByRequester_WithoutBudgetCategory()
    {
        var request = CreateDraftRequest();
        _repository.GetByIdAsync(_requestId, Arg.Any<CancellationToken>()).Returns(request);
        _currencyRepository.GetByCodeAsync("MMK", Arg.Any<CancellationToken>()).Returns(CreateActiveCurrency());
        _withdrawMethodRepository.GetByIdAsync(_withdrawMethodId, Arg.Any<CancellationToken>()).Returns(CreateWithdrawMethod(requiresAttachment: true));

        var command = new UpdateBudgetRequestCommand(
            _requestId,
            _requesterId,
            BudgetRequestType.Advance,
            DateTime.UtcNow.AddDays(2),
            200_000m,
            "MMK",
            "Updated detailed reasons",
            "Daw Mya",
            "Senior Accountant",
            _withdrawMethodId,
            true,
            "50% upfront, 50% completion",
            Note: "Updated note");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        request.Type.Should().Be(BudgetRequestType.Advance);
        request.RequestedAmount.Should().Be(200_000m);
        request.Reasons.Should().Be("Updated detailed reasons");
        request.WithdrawerName.Should().Be("Daw Mya");
        request.WithdrawerJobTitle.Should().Be("Senior Accountant");
        request.AllowsPartialPayment.Should().BeTrue();
        request.PartialPaymentDetail.Should().Be("50% upfront, 50% completion");

        await _modificationRepository.Received(1).AddAsync(
            Arg.Is<BudgetRequestModification>(m => m.BudgetRequestId == _requestId && m.ModifiedByUserId == _requesterId && m.Note == "Updated note"),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldSucceed_WhenValidSentBackUpdatedByRequester_WithActiveBudgetCategory()
    {
        var request = CreateDraftRequest();
        SubmitRequest(request);
        request.SendBack(_deptHeadId, "Please fix category");
        _repository.GetByIdAsync(_requestId, Arg.Any<CancellationToken>()).Returns(request);
        _currencyRepository.GetByCodeAsync("MMK", Arg.Any<CancellationToken>()).Returns(CreateActiveCurrency());
        _withdrawMethodRepository.GetByIdAsync(_withdrawMethodId, Arg.Any<CancellationToken>()).Returns(CreateWithdrawMethod(requiresAttachment: true));
        _budgetCategoryRepository.GetByIdAsync(_budgetCategoryId, Arg.Any<CancellationToken>()).Returns(CreateBudgetCategory());

        var command = new UpdateBudgetRequestCommand(
            _requestId,
            _requesterId,
            BudgetRequestType.Standard,
            DateTime.UtcNow.AddDays(1),
            150_000m,
            "MMK",
            "Fixed category reasons",
            "U Ba",
            "Officer",
            _withdrawMethodId,
            false,
            null,
            BudgetCategoryId: _budgetCategoryId);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        request.BudgetCategoryId.Should().Be(_budgetCategoryId);
        request.RequestedAmount.Should().Be(150_000m);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldSucceed_WhenValidPendingDeptHeadUpdatedByDeptHead()
    {
        var request = CreateDraftRequest();
        SubmitRequest(request);
        _repository.GetByIdAsync(_requestId, Arg.Any<CancellationToken>()).Returns(request);
        _currencyRepository.GetByCodeAsync("MMK", Arg.Any<CancellationToken>()).Returns(CreateActiveCurrency());
        _withdrawMethodRepository.GetByIdAsync(_withdrawMethodId, Arg.Any<CancellationToken>()).Returns(CreateWithdrawMethod(requiresAttachment: true));

        var command = new UpdateBudgetRequestCommand(
            _requestId,
            _deptHeadId,
            BudgetRequestType.Standard,
            DateTime.UtcNow.AddDays(1),
            90_000m, // Dept head adjusted amount
            "MMK",
            "Adjusted reasons by DeptHead",
            "U Ba",
            "Officer",
            _withdrawMethodId,
            false,
            null,
            Note: "Adjusted amount down by 10k");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        request.RequestedAmount.Should().Be(90_000m);
        await _modificationRepository.Received(1).AddAsync(
            Arg.Is<BudgetRequestModification>(m => m.BudgetRequestId == _requestId && m.ModifiedByUserId == _deptHeadId && m.Note == "Adjusted amount down by 10k"),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldClearWithdrawMethodAttachments_WhenWithdrawMethodDoesNotRequireAttachment()
    {
        var request = CreateDraftRequest();
        request.AddAttachment("account.png", "uploads/account.png", "image/png", 1024, _requesterId, AttachmentCategory.WithdrawMethod);
        _repository.GetByIdAsync(_requestId, Arg.Any<CancellationToken>()).Returns(request);
        _currencyRepository.GetByCodeAsync("MMK", Arg.Any<CancellationToken>()).Returns(CreateActiveCurrency());
        _withdrawMethodRepository.GetByIdAsync(_withdrawMethodId, Arg.Any<CancellationToken>()).Returns(CreateWithdrawMethod(requiresAttachment: false));

        var command = new UpdateBudgetRequestCommand(
            _requestId,
            _requesterId,
            BudgetRequestType.Standard,
            DateTime.UtcNow.AddDays(1),
            100_000m,
            "MMK",
            "Reasons",
            "U Ba",
            "Officer",
            _withdrawMethodId,
            false,
            null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        request.Attachments.Should().BeEmpty();
        await _fileStorage.Received(1).DeleteAsync("uploads/account.png", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldSucceedAndLogWarning_WhenFileStorageThrowsDuringAttachmentDeletion()
    {
        var request = CreateDraftRequest();
        request.AddAttachment("account.png", "uploads/account.png", "image/png", 1024, _requesterId, AttachmentCategory.WithdrawMethod);
        _repository.GetByIdAsync(_requestId, Arg.Any<CancellationToken>()).Returns(request);
        _currencyRepository.GetByCodeAsync("MMK", Arg.Any<CancellationToken>()).Returns(CreateActiveCurrency());
        _withdrawMethodRepository.GetByIdAsync(_withdrawMethodId, Arg.Any<CancellationToken>()).Returns(CreateWithdrawMethod(requiresAttachment: false));

        _fileStorage.DeleteAsync("uploads/account.png", Arg.Any<CancellationToken>())
            .ThrowsAsync(new IOException("Disk read-only"));

        var command = new UpdateBudgetRequestCommand(
            _requestId,
            _requesterId,
            BudgetRequestType.Standard,
            DateTime.UtcNow.AddDays(1),
            100_000m,
            "MMK",
            "Reasons",
            "U Ba",
            "Officer",
            _withdrawMethodId,
            false,
            null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldPropagateCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var command = new UpdateBudgetRequestCommand(
            _requestId,
            _requesterId,
            BudgetRequestType.Standard,
            DateTime.UtcNow.AddDays(1),
            100_000m,
            "MMK",
            "Reasons",
            "U Ba",
            "Officer",
            _withdrawMethodId,
            false,
            null);

        _repository.GetByIdAsync(_requestId, cts.Token)
            .ThrowsAsync(new OperationCanceledException());

        var act = () => _handler.Handle(command, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
