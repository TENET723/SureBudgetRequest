using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Entities;

namespace SureBudgetRequest.Application.WithdrawMethods.Commands.CreateWithdrawMethod;

public sealed record CreateWithdrawMethodCommand(
    string Name,
    bool RequiresAttachment = false) : IRequest<Result<Guid>>;

public sealed class CreateWithdrawMethodCommandHandler
    : IRequestHandler<CreateWithdrawMethodCommand, Result<Guid>>
{
    private readonly IWithdrawMethodRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateWithdrawMethodCommandHandler(IWithdrawMethodRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Guid>> Handle(CreateWithdrawMethodCommand command, CancellationToken ct)
    {
        var existing = await _repository.GetByNameAsync(command.Name, ct);
        if (existing is not null)
            return Result.Failure<Guid>($"Withdraw method '{existing.Name}' already exists.");

        WithdrawMethod method;
        try
        {
            method = new WithdrawMethod(command.Name, command.RequiresAttachment);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<Guid>(ex.Message);
        }

        await _repository.AddAsync(method, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success(method.Id);
    }
}
