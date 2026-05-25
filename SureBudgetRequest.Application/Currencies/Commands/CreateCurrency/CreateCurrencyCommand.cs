using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Errors;

namespace SureBudgetRequest.Application.Currencies.Commands.CreateCurrency;

public sealed record CreateCurrencyCommand(
    string Code,
    string Name,
    decimal RateToMmk) : IRequest<Result<string>>;

public sealed class CreateCurrencyCommandHandler
    : IRequestHandler<CreateCurrencyCommand, Result<string>>
{
    private readonly ICurrencyRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateCurrencyCommandHandler(ICurrencyRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<string>> Handle(CreateCurrencyCommand command, CancellationToken ct)
    {
        var existing = await _repository.GetByCodeAsync(command.Code, ct);
        if (existing is not null)
            return Result.Failure<string>(CurrencyErrors.AlreadyExists(command.Code));

        Currency currency;
        try
        {
            currency = new Currency(command.Code, command.Name, command.RateToMmk);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<string>(CurrencyErrors.ValidationError(ex.Message));
        }

        await _repository.AddAsync(currency, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(currency.Code);
    }
}
