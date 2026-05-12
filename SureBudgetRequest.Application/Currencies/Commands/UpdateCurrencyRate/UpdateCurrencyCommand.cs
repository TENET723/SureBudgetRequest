using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Entities;

namespace SureBudgetRequest.Application.Currencies.Commands.UpdateCurrencyRate;

/// <summary>
/// Updates a currency's display name and/or its rate. Any rate change writes a
/// <see cref="CurrencyRateChange"/> row in the same transaction for audit.
/// Also exposes IsActive toggling so the admin UI has a single place to manage everything.
/// </summary>
public sealed record UpdateCurrencyCommand(
    string Code,
    string Name,
    decimal RateToMmk,
    bool IsActive,
    Guid ChangedByUserId) : IRequest<Result>;

public sealed class UpdateCurrencyCommandHandler : IRequestHandler<UpdateCurrencyCommand, Result>
{
    private readonly ICurrencyRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateCurrencyCommandHandler(ICurrencyRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(UpdateCurrencyCommand command, CancellationToken ct)
    {
        var currency = await _repository.GetByCodeAsync(command.Code, ct);
        if (currency is null)
            return Result.Failure($"Currency '{command.Code}' not found.");

        try
        {
            currency.Rename(command.Name);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure(ex.Message);
        }

        // Rate change — only act if the value actually differs (avoid noisy audit rows)
        if (currency.RateToMmk != command.RateToMmk)
        {
            decimal previousRate;
            try
            {
                previousRate = currency.UpdateRate(command.RateToMmk);
            }
            catch (ArgumentException ex)
            {
                return Result.Failure(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Result.Failure(ex.Message);
            }

            var audit = new CurrencyRateChange(
                currency.Code, previousRate, currency.RateToMmk, command.ChangedByUserId);
            await _repository.AddRateChangeAsync(audit, ct);
        }

        // Active flag toggling
        if (currency.IsActive != command.IsActive)
        {
            try
            {
                if (command.IsActive) currency.Reactivate();
                else                  currency.Deactivate();
            }
            catch (InvalidOperationException ex)
            {
                return Result.Failure(ex.Message);
            }
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
