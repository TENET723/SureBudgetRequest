using FluentAssertions;
using NSubstitute;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.Abstractions.Services;
using SureBudgetRequest.Application.Vendors.Commands.CreateVendor;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;
using Xunit;

namespace SureBudgetRequest.Application.Tests.Vendors.Commands;

public class CreateVendorCommandHandlerTests
{
    private readonly IVendorRepository _repository = Substitute.For<IVendorRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IFileStorage _fileStorage = Substitute.For<IFileStorage>();
    private readonly CreateVendorCommandHandler _handler;

    public CreateVendorCommandHandlerTests()
    {
        _handler = new CreateVendorCommandHandler(_repository, _unitOfWork, _fileStorage);
    }

    [Fact]
    public async Task Handle_WhenValidVendorWithMultipleAccounts_CreatesVendorAndAccounts()
    {
        // Arrange
        _repository.GetByNameAsync("AnyCall", Arg.Any<CancellationToken>()).Returns((Vendor?)null);

        var accounts = new List<CreateVendorPaymentAccountDto>
        {
            new(PaymentAccountType.MobileWallet, "KBZPay", "09123456789", "U Thura"),
            new(PaymentAccountType.BankTransfer, "AYA Bank", "123456789", "Apex Tech", "Hledan Branch")
        };

        var command = new CreateVendorCommand(
            Name: "AnyCall",
            ContactPerson: "U Thura",
            InitialAccounts: accounts);

        Vendor? addedVendor = null;
        await _repository.AddAsync(Arg.Do<Vendor>(v => addedVendor = v), Arg.Any<CancellationToken>());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        addedVendor.Should().NotBeNull();
        addedVendor!.Name.Should().Be("AnyCall");
        addedVendor.PaymentAccounts.Should().HaveCount(2);

        var kbz = addedVendor.PaymentAccounts.First(a => a.ProviderName == "KBZPay");
        kbz.AccountNumber.Should().Be("09123456789");
        kbz.AccountHolderName.Should().Be("U Thura");

        var aya = addedVendor.PaymentAccounts.First(a => a.ProviderName == "AYA Bank");
        aya.AccountNumber.Should().Be("123456789");
        aya.BranchName.Should().Be("Hledan Branch");

        await _repository.Received(1).AddAsync(Arg.Any<Vendor>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenVendorWithSameNameExists_ReturnsFailure()
    {
        // Arrange
        var existing = new Vendor("AnyCall");
        _repository.GetByNameAsync("AnyCall", Arg.Any<CancellationToken>()).Returns(existing);

        var command = new CreateVendorCommand("AnyCall");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("already exists");

        await _repository.DidNotReceive().AddAsync(Arg.Any<Vendor>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
