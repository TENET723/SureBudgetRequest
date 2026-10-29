using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Application.Tags.Models;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;

namespace SureBudgetRequest.Application.BudgetRequests.Commands.AddTagToRequest;

public sealed record AddTagToRequestCommand(
    Guid BudgetRequestId,
    Guid TagId,
    Guid ActorUserId) : IRequest<Result>;

public sealed class AddTagToRequestCommandHandler
    : IRequestHandler<AddTagToRequestCommand, Result>
{
    private readonly IBudgetRequestRepository _requestRepository;
    private readonly ITagRepository _tagRepository;
    private readonly IUserRepository _userRepository;
    private readonly IDepartmentRepository _departmentRepository;
    private readonly IAppSettingRepository _appSettingRepository;
    private readonly IBudgetRequestModificationRepository _modificationRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AddTagToRequestCommandHandler(
        IBudgetRequestRepository requestRepository,
        ITagRepository tagRepository,
        IUserRepository userRepository,
        IDepartmentRepository departmentRepository,
        IAppSettingRepository appSettingRepository,
        IBudgetRequestModificationRepository modificationRepository,
        IUnitOfWork unitOfWork)
    {
        _requestRepository = requestRepository;
        _tagRepository = tagRepository;
        _userRepository = userRepository;
        _departmentRepository = departmentRepository;
        _appSettingRepository = appSettingRepository;
        _modificationRepository = modificationRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(AddTagToRequestCommand command, CancellationToken ct)
    {
        var request = await _requestRepository.GetByIdAsync(command.BudgetRequestId, ct);
        if (request is null)
            return Result.Failure(BudgetRequestErrors.NotFound(command.BudgetRequestId));

        var actor = await _userRepository.GetByIdAsync(command.ActorUserId, ct);
        if (actor is null || !actor.IsActive)
            return Result.Failure(UserErrors.NotFound(command.ActorUserId));

        if (actor.Role == UserRole.Support)
            return Result.Failure(TagErrors.CannotAttachTag);

        var configSetting = await _appSettingRepository.GetByKeyAsync(TagVisibilityConfig.SettingKey, ct);
        var config = TagVisibilityConfig.FromJson(configSetting?.Value);

        bool isAllowed = actor.Role == UserRole.Admin
            || actor.Id == request.RequesterId
            || (request.DepartmentIdAtSubmission != Guid.Empty && actor.DepartmentId == request.DepartmentIdAtSubmission)
            || actor.Role == UserRole.Finance
            || (config.FinanceDepartmentId.HasValue && actor.DepartmentId == config.FinanceDepartmentId.Value)
            || actor.Role == UserRole.Management
            || (config.ManagementDepartmentId.HasValue && actor.DepartmentId == config.ManagementDepartmentId.Value)
            || actor.Role == UserRole.Accounting
            || (config.AccountingDepartmentId.HasValue && actor.DepartmentId == config.AccountingDepartmentId.Value);

        if (!isAllowed)
            return Result.Failure(TagErrors.CannotAttachTag);

        var tag = await _tagRepository.GetByIdAsync(command.TagId, ct);
        if (tag is null)
            return Result.Failure(TagErrors.NotFound);

        int maxLimit = 5;
        var setting = await _appSettingRepository.GetByKeyAsync("TagSettings:MaxTagsPerDepartmentOnTicket", ct);
        if (setting is not null && int.TryParse(setting.Value, out var parsed) && parsed > 0)
        {
            maxLimit = parsed;
        }

        var result = request.AddTag(tag, actor.Id, actor.DepartmentId, maxLimit);
        if (result.IsFailure)
            return result;

        var dept = await _departmentRepository.GetByIdAsync(tag.DepartmentId, ct);
        var deptName = dept?.Name ?? "Department";

        await _modificationRepository.AddAsync(
            new BudgetRequestModification(request.Id, actor.Id, $"Added tag '{tag.Name}' ({deptName})"),
            ct);

        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
