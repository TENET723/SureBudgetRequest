using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Entities;

namespace SureBudgetRequest.Application.BudgetRequests.Commands.CreateDraft;

public sealed record CreateDraftCommand(
    Guid RequesterId,
    DateTime RequestDate,
    decimal RequestedAmount,
    string Reasons,
    string WithdrawerName,
    string WithdrawerJobTitle,
    bool AllowsPartialPayment,
    string? PartialPaymentDetail) : IRequest<Result<Guid>>;

public sealed class CreateDraftCommandHandler
    : IRequestHandler<CreateDraftCommand, Result<Guid>>
{
    private readonly IBudgetRequestRepository _budgetRequestRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateDraftCommandHandler(
        IBudgetRequestRepository budgetRequestRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork)
    {
        _budgetRequestRepository = budgetRequestRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Guid>> Handle(
        CreateDraftCommand command,
        CancellationToken cancellationToken)
    {
        var requester = await _userRepository.GetByIdAsync(command.RequesterId, cancellationToken);
        if (requester is null)
            return Result.Failure<Guid>("Requester not found.");

        if (!requester.IsActive)
            return Result.Failure<Guid>("Inactive users cannot create requests.");

        BudgetRequest draft;
        try
        {
            draft = BudgetRequest.CreateDraft(
                command.RequesterId,
                command.RequestDate,
                command.RequestedAmount,
                command.Reasons,
                command.WithdrawerName,
                command.WithdrawerJobTitle,
                command.AllowsPartialPayment,
                command.PartialPaymentDetail);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<Guid>(ex.Message);
        }

        await _budgetRequestRepository.AddAsync(draft, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(draft.Id);
    }
}
