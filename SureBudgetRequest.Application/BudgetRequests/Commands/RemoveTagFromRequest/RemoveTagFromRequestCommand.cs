using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Errors;

namespace SureBudgetRequest.Application.BudgetRequests.Commands.RemoveTagFromRequest;

public sealed record RemoveTagFromRequestCommand(
    Guid BudgetRequestId,
    Guid TagId,
    Guid ActorUserId) : IRequest<Result>;

public sealed class RemoveTagFromRequestCommandHandler
    : IRequestHandler<RemoveTagFromRequestCommand, Result>
{
    private readonly IBudgetRequestRepository _requestRepository;
    private readonly ITagRepository _tagRepository;
    private readonly IUserRepository _userRepository;
    private readonly IDepartmentRepository _departmentRepository;
    private readonly IBudgetRequestModificationRepository _modificationRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RemoveTagFromRequestCommandHandler(
        IBudgetRequestRepository requestRepository,
        ITagRepository tagRepository,
        IUserRepository userRepository,
        IDepartmentRepository departmentRepository,
        IBudgetRequestModificationRepository modificationRepository,
        IUnitOfWork unitOfWork)
    {
        _requestRepository = requestRepository;
        _tagRepository = tagRepository;
        _userRepository = userRepository;
        _departmentRepository = departmentRepository;
        _modificationRepository = modificationRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(RemoveTagFromRequestCommand command, CancellationToken ct)
    {
        var request = await _requestRepository.GetByIdAsync(command.BudgetRequestId, ct);
        if (request is null)
            return Result.Failure(BudgetRequestErrors.NotFound(command.BudgetRequestId));

        var actor = await _userRepository.GetByIdAsync(command.ActorUserId, ct);
        if (actor is null || !actor.IsActive)
            return Result.Failure(UserErrors.NotFound(command.ActorUserId));

        var tag = await _tagRepository.GetByIdAsync(command.TagId, ct);
        if (tag is null)
            return Result.Failure(TagErrors.NotFound);

        var result = request.RemoveTag(tag.Id, actor.Id, actor.DepartmentId);
        if (result.IsFailure)
            return result;

        var dept = await _departmentRepository.GetByIdAsync(tag.DepartmentId, ct);
        var deptName = dept?.Name ?? "Department";

        await _modificationRepository.AddAsync(
            new BudgetRequestModification(request.Id, actor.Id, $"Removed tag '{tag.Name}' ({deptName})"),
            ct);

        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
