using MediatR;
using SureBudgetRequest.Application.Abstractions;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Errors;

namespace SureBudgetRequest.Application.Departments.Commands.UpdateDepartment;

public sealed record UpdateDepartmentCommand(
    Guid DepartmentId,
    string Name,
    Guid? HeadUserId,
    decimal BudgetLimit,
    decimal? MonthlyLimit = null,
    string? SlackWebhookUrl = null) : IRequest<Result>;

public sealed class UpdateDepartmentCommandHandler
    : IRequestHandler<UpdateDepartmentCommand, Result>
{
    private readonly IDepartmentRepository _departmentRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateDepartmentCommandHandler(
        IDepartmentRepository departmentRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork)
    {
        _departmentRepository = departmentRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(UpdateDepartmentCommand command, CancellationToken ct)
    {
        var dept = await _departmentRepository.GetByIdAsync(command.DepartmentId, ct);
        if (dept is null) return Result.Failure(DepartmentErrors.NotFound);

        // Only validate the head user exists when one was provided.
        // Passing null clears the head (e.g. position vacant).
        if (command.HeadUserId.HasValue)
        {
            var head = await _userRepository.GetByIdAsync(command.HeadUserId.Value, ct);
            if (head is null) return Result.Failure(DepartmentErrors.HeadNotFound);
        }

        try
        {
            dept.Rename(command.Name);
            dept.ChangeHead(command.HeadUserId);
            dept.ChangeBudgetLimit(command.BudgetLimit);
            dept.ChangeMonthlyLimit(command.MonthlyLimit);
            dept.SetSlackWebhookUrl(command.SlackWebhookUrl);
        }
        catch (ArgumentException ex)
        {
            // Surface domain validation (e.g. malformed webhook URL) as a user-facing failure.
            return Result.Failure(DepartmentErrors.ValidationError(ex.Message));
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
