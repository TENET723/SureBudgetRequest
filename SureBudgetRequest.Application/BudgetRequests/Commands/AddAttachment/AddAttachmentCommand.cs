using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;

namespace SureBudgetRequest.Application.BudgetRequests.Commands.AddAttachment;

public sealed record AddAttachmentCommand(
    Guid BudgetRequestId,
    Guid UploadedByUserId,
    string FileName,
    string StoredPath,
    string ContentType,
    long SizeBytes) : IRequest<Result>;

public sealed class AddAttachmentCommandHandler
    : IRequestHandler<AddAttachmentCommand, Result>
{
    private readonly IBudgetRequestRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public AddAttachmentCommandHandler(
        IBudgetRequestRepository repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(
        AddAttachmentCommand command,
        CancellationToken cancellationToken)
    {
        var budgetRequest = await _repository.GetByIdAsync(
            command.BudgetRequestId, cancellationToken);
        if (budgetRequest is null)
            return Result.Failure("Budget request not found.");

        var result = budgetRequest.AddAttachment(
            command.FileName,
            command.StoredPath,
            command.ContentType,
            command.SizeBytes,
            command.UploadedByUserId);

        if (result.IsFailure) return result;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
