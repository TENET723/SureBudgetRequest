using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;

namespace SureBudgetRequest.Application.Tags.Commands.UpdateTag;

public sealed record UpdateTagCommand(
    Guid TagId,
    string NewName,
    string? NewColorHex,
    Guid ActorUserId,
    string? NewFontColorHex = null) : IRequest<Result>;

public sealed class UpdateTagCommandHandler
    : IRequestHandler<UpdateTagCommand, Result>
{
    private readonly ITagRepository _tagRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateTagCommandHandler(
        ITagRepository tagRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork)
    {
        _tagRepository = tagRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(UpdateTagCommand command, CancellationToken ct)
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
            return Result.Failure(Error.Forbidden("Tag.UnauthorizedUpdate", "Only members of the department or an admin can update this tag."));

        if (!string.Equals(tag.Name, command.NewName?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            var existing = await _tagRepository.GetByNameAndDepartmentAsync(command.NewName!, tag.DepartmentId, ct);
            if (existing is not null && existing.Id != tag.Id)
                return Result.Failure(TagErrors.AlreadyExists(existing.Name));
        }

        try
        {
            tag.Rename(command.NewName!);
            tag.SetColors(command.NewColorHex, command.NewFontColorHex);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure(TagErrors.ValidationError(ex.Message));
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
