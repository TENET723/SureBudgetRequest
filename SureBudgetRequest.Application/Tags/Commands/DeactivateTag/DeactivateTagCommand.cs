using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;

namespace SureBudgetRequest.Application.Tags.Commands.DeactivateTag;

public sealed record DeactivateTagCommand(Guid TagId, Guid ActorUserId) : IRequest<Result>;

public sealed class DeactivateTagCommandHandler
    : IRequestHandler<DeactivateTagCommand, Result>
{
    private readonly ITagRepository _tagRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeactivateTagCommandHandler(
        ITagRepository tagRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork)
    {
        _tagRepository = tagRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(DeactivateTagCommand command, CancellationToken ct)
    {
        var tag = await _tagRepository.GetByIdAsync(command.TagId, ct);
        if (tag is null)
            return Result.Failure(TagErrors.NotFound);

        var actor = await _userRepository.GetByIdAsync(command.ActorUserId, ct);
        if (actor is null || !actor.IsActive)
            return Result.Failure(UserErrors.NotFound(command.ActorUserId));

        bool isAuthorized = actor.Role == UserRole.Admin ||
            actor.DepartmentId == tag.DepartmentId;

        if (!isAuthorized)
            return Result.Failure(Error.Forbidden("Tag.UnauthorizedUpdate", "Only members of the department or an admin can deactivate this tag."));

        tag.Deactivate();
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
