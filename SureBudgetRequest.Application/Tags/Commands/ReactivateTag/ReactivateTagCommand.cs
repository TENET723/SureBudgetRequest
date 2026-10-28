using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;

namespace SureBudgetRequest.Application.Tags.Commands.ReactivateTag;

public sealed record ReactivateTagCommand(Guid TagId, Guid ActorUserId) : IRequest<Result>;

public sealed class ReactivateTagCommandHandler
    : IRequestHandler<ReactivateTagCommand, Result>
{
    private readonly ITagRepository _tagRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ReactivateTagCommandHandler(
        ITagRepository tagRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork)
    {
        _tagRepository = tagRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(ReactivateTagCommand command, CancellationToken ct)
    {
        var tag = await _tagRepository.GetByIdAsync(command.TagId, ct);
        if (tag is null)
            return Result.Failure(TagErrors.NotFound);

        var actor = await _userRepository.GetByIdAsync(command.ActorUserId, ct);
        if (actor is null || !actor.IsActive)
            return Result.Failure(UserErrors.NotFound(command.ActorUserId));

        bool isAuthorized = actor.Role == UserRole.Admin ||
            (actor.Role == UserRole.DepartmentHead && actor.DepartmentId == tag.DepartmentId);

        if (!isAuthorized)
            return Result.Failure(Error.Forbidden("Tag.UnauthorizedUpdate", "Only the department head or an admin can reactivate this tag."));

        tag.Reactivate();
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
