using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Entities;

namespace SureBudgetRequest.Application.AppSettings.Commands.CreateAppSetting;

public sealed record CreateAppSettingCommand(
    string Key,
    string Value,
    string? Description) : IRequest<Result>;

public sealed class CreateAppSettingCommandHandler : IRequestHandler<CreateAppSettingCommand, Result>
{
    private readonly IAppSettingRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateAppSettingCommandHandler(IAppSettingRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(CreateAppSettingCommand command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.Key))
            return Result.Failure("Key is required.");

        var existing = await _repository.GetByKeyAsync(command.Key, ct);
        if (existing is not null)
            return Result.Failure($"A setting with key '{command.Key}' already exists.");

        var setting = new AppSetting(command.Key, command.Value, command.Description);
        await _repository.AddAsync(setting, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }
}
