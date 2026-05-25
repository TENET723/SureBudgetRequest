using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Errors;

namespace SureBudgetRequest.Application.WithdrawMethods.Commands.UpdateWithdrawMethod;

public sealed record UpdateWithdrawMethodCommand(
    Guid WithdrawMethodId,
    string Name,
    bool IsActive,
    bool RequiresAttachment) : IRequest<Result>;

public sealed class UpdateWithdrawMethodCommandHandler
    : IRequestHandler<UpdateWithdrawMethodCommand, Result>
{
    private readonly IWithdrawMethodRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateWithdrawMethodCommandHandler(IWithdrawMethodRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(UpdateWithdrawMethodCommand command, CancellationToken ct)
    {
        var method = await _repository.GetByIdAsync(command.WithdrawMethodId, ct);
        if (method is null) return Result.Failure(WithdrawMethodErrors.GenericNotFound);

        // If the name is being changed, ensure the new name isn't taken by another row.
        if (!string.Equals(method.Name, command.Name?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            var existing = await _repository.GetByNameAsync(command.Name ?? string.Empty, ct);
            if (existing is not null && existing.Id != method.Id)
                return Result.Failure(WithdrawMethodErrors.AlreadyExists(existing.Name));
        }

        try
        {
            method.Rename(command.Name);
            method.SetRequiresAttachment(command.RequiresAttachment);
            if (command.IsActive) method.Reactivate(); else method.Deactivate();
        }
        catch (ArgumentException ex)
        {
            return Result.Failure(WithdrawMethodErrors.ValidationError(ex.Message));
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}