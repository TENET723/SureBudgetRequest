using MediatR;
using SureBudgetRequest.Application.Abstractions.Repositories;
using SureBudgetRequest.Domain.Common;
using SureBudgetRequest.Domain.Entities;
using SureBudgetRequest.Domain.Enums;
using SureBudgetRequest.Domain.Errors;

namespace SureBudgetRequest.Application.Vendors.Queries;

public sealed record VendorPaymentAccountDto(
    Guid Id,
    Guid VendorId,
    PaymentAccountType AccountType,
    string ProviderName,
    string AccountNumber,
    string AccountHolderName,
    string? BranchName,
    bool IsActive,
    DateTime CreatedAt,
    string? AttachmentFileName,
    string? AttachmentStoredPath,
    string? AttachmentContentType,
    long? AttachmentSizeBytes)
{
    public bool HasAttachment => !string.IsNullOrEmpty(AttachmentStoredPath);

    public static VendorPaymentAccountDto FromEntity(VendorPaymentAccount a) =>
        new(
            a.Id,
            a.VendorId,
            a.AccountType,
            a.ProviderName,
            a.AccountNumber,
            a.AccountHolderName,
            a.BranchName,
            a.IsActive,
            a.CreatedAt,
            a.AttachmentFileName,
            a.AttachmentStoredPath,
            a.AttachmentContentType,
            a.AttachmentSizeBytes);
}

public sealed record VendorDto(
    Guid Id,
    string Name,
    string? ContactPerson,
    string? Phone,
    string? Email,
    string? Address,
    bool IsActive,
    DateTime CreatedAt,
    IReadOnlyList<VendorPaymentAccountDto> PaymentAccounts)
{
    public static VendorDto FromEntity(Vendor v) =>
        new(
            v.Id,
            v.Name,
            v.ContactPerson,
            v.Phone,
            v.Email,
            v.Address,
            v.IsActive,
            v.CreatedAt,
            v.PaymentAccounts.Select(VendorPaymentAccountDto.FromEntity).ToList());
}

// ── Get single ─────────────────────────────────────────────────────────────

public sealed record GetVendorByIdQuery(Guid VendorId) : IRequest<Result<VendorDto>>;

public sealed class GetVendorByIdQueryHandler : IRequestHandler<GetVendorByIdQuery, Result<VendorDto>>
{
    private readonly IVendorRepository _repository;

    public GetVendorByIdQueryHandler(IVendorRepository repository) => _repository = repository;

    public async Task<Result<VendorDto>> Handle(GetVendorByIdQuery request, CancellationToken ct)
    {
        var vendor = await _repository.GetByIdAsync(request.VendorId, ct);
        return vendor is null
            ? Result.Failure<VendorDto>(VendorErrors.GenericNotFound)
            : Result.Success(VendorDto.FromEntity(vendor));
    }
}

// ── List ───────────────────────────────────────────────────────────────────

public sealed record ListVendorsQuery(bool IncludeInactive = false)
    : IRequest<Result<IReadOnlyList<VendorDto>>>;

public sealed class ListVendorsQueryHandler
    : IRequestHandler<ListVendorsQuery, Result<IReadOnlyList<VendorDto>>>
{
    private readonly IVendorRepository _repository;

    public ListVendorsQueryHandler(IVendorRepository repository) => _repository = repository;

    public async Task<Result<IReadOnlyList<VendorDto>>> Handle(ListVendorsQuery request, CancellationToken ct)
    {
        var items = await _repository.ListAsync(request.IncludeInactive, ct);
        return Result.Success<IReadOnlyList<VendorDto>>(items.Select(VendorDto.FromEntity).ToList());
    }
}

// ── Download attachment ───────────────────────────────────────────────────

public sealed record DownloadVendorAccountAttachmentResult(
    string FileName,
    string ContentType,
    Stream Stream);

public sealed record DownloadVendorAccountAttachmentQuery(
    Guid VendorId,
    Guid PaymentAccountId) : IRequest<Result<DownloadVendorAccountAttachmentResult>>;

public sealed class DownloadVendorAccountAttachmentQueryHandler
    : IRequestHandler<DownloadVendorAccountAttachmentQuery, Result<DownloadVendorAccountAttachmentResult>>
{
    private readonly IVendorRepository _repository;
    private readonly SureBudgetRequest.Application.Abstractions.Services.IFileStorage _fileStorage;

    public DownloadVendorAccountAttachmentQueryHandler(
        IVendorRepository repository,
        SureBudgetRequest.Application.Abstractions.Services.IFileStorage fileStorage)
    {
        _repository = repository;
        _fileStorage = fileStorage;
    }

    public async Task<Result<DownloadVendorAccountAttachmentResult>> Handle(
        DownloadVendorAccountAttachmentQuery request,
        CancellationToken ct)
    {
        var vendor = await _repository.GetByIdAsync(request.VendorId, ct);
        if (vendor is null)
            return Result.Failure<DownloadVendorAccountAttachmentResult>(VendorErrors.GenericNotFound);

        var account = vendor.PaymentAccounts.FirstOrDefault(a => a.Id == request.PaymentAccountId);
        if (account is null || !account.HasAttachment)
            return Result.Failure<DownloadVendorAccountAttachmentResult>("Attachment not found.");

        try
        {
            var stream = await _fileStorage.ReadAsync(account.AttachmentStoredPath!, ct);
            return Result.Success(new DownloadVendorAccountAttachmentResult(
                account.AttachmentFileName ?? "attachment",
                account.AttachmentContentType ?? "application/octet-stream",
                stream));
        }
        catch (Exception ex)
        {
            return Result.Failure<DownloadVendorAccountAttachmentResult>($"Failed to read file: {ex.Message}");
        }
    }
}
