using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Reconciliations.Commands;

public sealed record AddReconciliationDetailCommand(
    Guid ReconciliationId,
    string SourceType,
    Guid SourceId,
    string? TargetType,
    Guid? TargetId,
    decimal SourceAmount,
    decimal TargetAmount,
    decimal MatchedAmount,
    string CurrencyCode,
    string? Notes) : IRequest<Guid>;

public sealed class AddReconciliationDetailCommandValidator : AbstractValidator<AddReconciliationDetailCommand>
{
    private static readonly HashSet<string> AllowedObjectTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ReconciliationObjectTypes.Payment,
        ReconciliationObjectTypes.Collection,
        ReconciliationObjectTypes.Cost,
        ReconciliationObjectTypes.Revenue,
        ReconciliationObjectTypes.Document,
        ReconciliationObjectTypes.AccountsPayable,
        ReconciliationObjectTypes.AccountsReceivable,
        ReconciliationObjectTypes.Other
    };

    public AddReconciliationDetailCommandValidator()
    {
        RuleFor(x => x.ReconciliationId).NotEmpty().WithMessage("Phiên đối soát không hợp lệ.");
        RuleFor(x => x.SourceType)
            .NotEmpty().WithMessage("Loại nguồn đối soát không được để trống.")
            .Must(t => AllowedObjectTypes.Contains(t.Trim()))
            .WithMessage("Loại nguồn đối soát không hợp lệ.");
        RuleFor(x => x.SourceId).NotEmpty().WithMessage("Đối tượng nguồn không hợp lệ.");
        RuleFor(x => x.TargetType)
            .Must(t => t is null || AllowedObjectTypes.Contains(t.Trim()))
            .WithMessage("Loại đích đối soát không hợp lệ.");
        RuleFor(x => x.SourceAmount)
            .GreaterThanOrEqualTo(0).WithMessage("Số tiền nguồn không được âm.");
        RuleFor(x => x.TargetAmount)
            .GreaterThanOrEqualTo(0).WithMessage("Số tiền đích không được âm.");
        RuleFor(x => x.MatchedAmount)
            .GreaterThanOrEqualTo(0).WithMessage("Số tiền khớp không được âm.");
        RuleFor(x => x.CurrencyCode)
            .NotEmpty().WithMessage("Mã tiền tệ không được để trống.")
            .Length(3).WithMessage("Mã tiền tệ phải gồm 3 ký tự.");
        RuleFor(x => x.Notes).MaximumLength(2048).When(x => x.Notes is not null);
        RuleFor(x => x)
            .Must(x => !(x.TargetId.HasValue ^ !string.IsNullOrWhiteSpace(x.TargetType)))
            .WithMessage("Đích đối soát phải có đủ loại và mã, hoặc cả hai để trống.");
    }
}

/// <summary>
/// Adds reconciliation detail. When variance amount ≠ 0, creates a Variance control fact —
/// does NOT open an Exception (Variance ≠ Exception). Severity from amount thresholds.
/// </summary>
public sealed class AddReconciliationDetailCommandHandler : IRequestHandler<AddReconciliationDetailCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IReconciliationDetailWriter _writer;

    public AddReconciliationDetailCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IReconciliationDetailWriter writer)
    {
        _db = db;
        _tenantContext = tenantContext;
        _writer = writer;
    }

    public async Task<Guid> Handle(AddReconciliationDetailCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var tenantId = _tenantContext.TenantId!.Value;
        var session = await _db.Reconciliations
            .FirstOrDefaultAsync(r => r.Id == request.ReconciliationId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy phiên đối soát.");

        var exceptionCountBefore = await _db.Exceptions.CountAsync(cancellationToken);

        var detailId = await _writer.AddAsync(
            session,
            tenantId,
            request.SourceType,
            request.SourceId,
            request.TargetType,
            request.TargetId,
            request.SourceAmount,
            request.TargetAmount,
            request.MatchedAmount,
            request.CurrencyCode,
            request.Notes,
            cancellationToken);

        var exceptionCountAfter = await _db.Exceptions.CountAsync(cancellationToken);
        if (exceptionCountAfter != exceptionCountBefore)
        {
            throw new ConflictAppException(
                "Chi tiết đối soát không được tự tạo Ngoại lệ — Chênh lệch ≠ Ngoại lệ.");
        }

        return detailId;
    }
}
