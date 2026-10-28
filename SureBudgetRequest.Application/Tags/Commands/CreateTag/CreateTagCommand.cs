using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;

namespace SureBudgetRequest.Application.Tags.Commands.CreateTag;

public sealed record CreateTagCommand(
    Guid DepartmentId,
    string Name,
    Guid CreatedByUserId,
    string? ColorHex = null,
    string? FontColorHex = null) : IRequest<Result<Guid>>;

public sealed class CreateTagCommandHandler
    : IRequestHandler<CreateTagCommand, Result<Guid>>
{
    private readonly ITagRepository _tagRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateTagCommandHandler(
        ITagRepository tagRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork)
    {
        _tagRepository = tagRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Guid>> Handle(CreateTagCommand command, CancellationToken ct)
    {
        var user = await _userRepository.GetByIdAsync(command.CreatedByUserId, ct);
        if (user is null || !user.IsActive)
            return Result.Failure<Guid>(UserErrors.NotFound(command.CreatedByUserId));

        if (user.Role != UserRole.Admin && user.DepartmentId != command.DepartmentId)
            return Result.Failure<Guid>(TagErrors.CrossDepartmentNotAllowed);

        var existing = await _tagRepository.GetByNameAndDepartmentAsync(command.Name, command.DepartmentId, ct);
        if (existing is not null)
            return Result.Failure<Guid>(TagErrors.AlreadyExists(existing.Name));

        Tag tag;
        try
        {
            tag = new Tag(command.DepartmentId, command.Name, command.CreatedByUserId, command.ColorHex, command.FontColorHex);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<Guid>(TagErrors.ValidationError(ex.Message));
        }

        await _tagRepository.AddAsync(tag, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(tag.Id);
    }
}
