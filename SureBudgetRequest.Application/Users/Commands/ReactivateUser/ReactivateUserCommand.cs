using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Errors;

namespace SureBudgetRequest.Application.Users.Commands.ReactivateUser;

public sealed record ReactivateUserCommand(Guid UserId) : IRequest<Result>;

public sealed class ReactivateUserCommandHandler : IRequestHandler<ReactivateUserCommand, Result>
{
    private readonly IUserRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public ReactivateUserCommandHandler(IUserRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(ReactivateUserCommand command, CancellationToken ct)
    {
        var user = await _repository.GetByIdAsync(command.UserId, ct);
        if (user is null) return Result.Failure(UserErrors.GenericNotFound);

        user.Reactivate();
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
