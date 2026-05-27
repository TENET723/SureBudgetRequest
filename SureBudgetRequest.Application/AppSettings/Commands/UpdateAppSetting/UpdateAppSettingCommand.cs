using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;

namespace SureBudgetRequest.Application.AppSettings.Commands.UpdateAppSetting;

public sealed record UpdateAppSettingCommand(
    string Key,
    string Value,
    string? Description) : IRequest<Result>;

public sealed class UpdateAppSettingCommandHandler : IRequestHandler<UpdateAppSettingCommand, Result>
{
    private readonly IAppSettingRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateAppSettingCommandHandler(IAppSettingRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(UpdateAppSettingCommand command, CancellationToken ct)
    {
        var setting = await _repository.GetByKeyAsync(command.Key, ct);
        if (setting is null)
            return Result.Failure($"App setting '{command.Key}' not found.");

        setting.UpdateValue(command.Value);
        setting.UpdateDescription(command.Description);

        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
