using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Enums;

namespace SureBudgetRequest.Application.Users.Commands.DeactivateUser;

public sealed record DeactivateUserCommand(Guid UserId) : IRequest<Result>;

public sealed class DeactivateUserCommandHandler : IRequestHandler<DeactivateUserCommand, Result>
{
    private readonly IUserRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public DeactivateUserCommandHandler(IUserRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(DeactivateUserCommand command, CancellationToken ct)
    {
        var user = await _repository.GetByIdAsync(command.UserId, ct);
        if (user is null) return Result.Failure("User not found.");

        // Bus-factor safeguard: refuse to deactivate the last active Finance Approver.
        if (user.IsActive && user.Role == UserRole.Finance && user.IsFinanceApprover)
        {
            var approverCount = await _repository.CountActiveFinanceApproversAsync(ct);
            if (approverCount <= 1)
            {
                return Result.Failure(
                    "Cannot deactivate the last active Finance Approver — Finance-stage requests " +
                    "would have no one to approve them. Promote another Finance user first.");
            }
        }

        user.Deactivate();
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
