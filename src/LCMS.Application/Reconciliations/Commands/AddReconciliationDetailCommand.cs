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
/// does NOT open an Exception (Variance ≠ Exception).
/// </summary>
public sealed class AddReconciliationDetailCommandHandler : IRequestHandler<AddReconciliationDetailCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public AddReconciliationDetailCommandHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
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

        if (string.Equals(session.Status, ReconciliationStatuses.Completed, StringComparison.OrdinalIgnoreCase)
            || string.Equals(session.Status, ReconciliationStatuses.Cancelled, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictAppException("Không thể thêm chi tiết vào phiên đối soát đã hoàn tất hoặc đã hủy.");
        }

        var sourceAmount = decimal.Round(request.SourceAmount, 4, MidpointRounding.AwayFromZero);
        var targetAmount = decimal.Round(request.TargetAmount, 4, MidpointRounding.AwayFromZero);
        var matchedAmount = decimal.Round(request.MatchedAmount, 4, MidpointRounding.AwayFromZero);
        if (matchedAmount > sourceAmount || matchedAmount > targetAmount)
        {
            throw new ConflictAppException("Số tiền khớp không được vượt số tiền nguồn hoặc đích.");
        }

        var varianceAmount = decimal.Round(sourceAmount - matchedAmount, 4, MidpointRounding.AwayFromZero);
        var lineStatus = varianceAmount == 0m && matchedAmount > 0m
            ? ReconciliationDetailStatuses.Matched
            : varianceAmount != 0m
                ? ReconciliationDetailStatuses.Variance
                : ReconciliationDetailStatuses.Unmatched;

        var sourceType = request.SourceType.Trim().ToLowerInvariant();
        var targetType = string.IsNullOrWhiteSpace(request.TargetType)
            ? null
            : request.TargetType.Trim().ToLowerInvariant();
        var currency = request.CurrencyCode.Trim().ToUpperInvariant();

        await EnsureObjectExistsAsync(sourceType, request.SourceId, cancellationToken);
        if (request.TargetId.HasValue && targetType is not null)
        {
            await EnsureObjectExistsAsync(targetType, request.TargetId.Value, cancellationToken);
        }

        var exceptionCountBefore = await _db.Exceptions.CountAsync(cancellationToken);

        var detail = new ReconciliationDetail
        {
            TenantId = tenantId,
            ReconciliationId = session.Id,
            SourceType = sourceType,
            SourceId = request.SourceId,
            TargetType = targetType,
            TargetId = request.TargetId,
            SourceAmount = sourceAmount,
            TargetAmount = targetAmount,
            MatchedAmount = matchedAmount,
            VarianceAmount = varianceAmount,
            CurrencyCode = currency,
            LineStatus = lineStatus,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim()
        };

        _db.ReconciliationDetails.Add(detail);
        await _db.SaveChangesAsync(cancellationToken);

        if (varianceAmount != 0m)
        {
            var variance = new Variance
            {
                TenantId = tenantId,
                ReconciliationId = session.Id,
                ReconciliationDetailId = detail.Id,
                VarianceType = VarianceTypes.Amount,
                Amount = varianceAmount,
                CurrencyCode = currency,
                SourceType = sourceType,
                SourceId = request.SourceId,
                TargetType = targetType,
                TargetId = request.TargetId,
                Status = VarianceStatuses.Open,
                Explanation = null,
                ExceptionId = null
            };
            _db.Variances.Add(variance);
            await _db.SaveChangesAsync(cancellationToken);

            detail.VarianceId = variance.Id;
            await _db.SaveChangesAsync(cancellationToken);
        }

        var exceptionCountAfter = await _db.Exceptions.CountAsync(cancellationToken);
        if (exceptionCountAfter != exceptionCountBefore)
        {
            throw new ConflictAppException(
                "Chi tiết đối soát không được tự tạo Ngoại lệ — Chênh lệch ≠ Ngoại lệ.");
        }

        if (string.Equals(session.Status, ReconciliationStatuses.Draft, StringComparison.OrdinalIgnoreCase))
        {
            session.Status = ReconciliationStatuses.InProgress;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return detail.Id;
    }

    private async Task EnsureObjectExistsAsync(string objectType, Guid objectId, CancellationToken cancellationToken)
    {
        var exists = objectType switch
        {
            ReconciliationObjectTypes.Payment => await _db.Payments.AsNoTracking().AnyAsync(x => x.Id == objectId, cancellationToken),
            ReconciliationObjectTypes.Collection => await _db.Collections.AsNoTracking().AnyAsync(x => x.Id == objectId, cancellationToken),
            ReconciliationObjectTypes.Cost => await _db.Costs.AsNoTracking().AnyAsync(x => x.Id == objectId, cancellationToken),
            ReconciliationObjectTypes.Revenue => await _db.Revenues.AsNoTracking().AnyAsync(x => x.Id == objectId, cancellationToken),
            ReconciliationObjectTypes.Document => await _db.FinancialDocuments.AsNoTracking().AnyAsync(x => x.Id == objectId, cancellationToken),
            ReconciliationObjectTypes.AccountsPayable => await _db.AccountsPayable.AsNoTracking().AnyAsync(x => x.Id == objectId, cancellationToken),
            ReconciliationObjectTypes.AccountsReceivable => await _db.AccountsReceivable.AsNoTracking().AnyAsync(x => x.Id == objectId, cancellationToken),
            ReconciliationObjectTypes.Other => true,
            _ => false
        };

        if (!exists)
        {
            throw new NotFoundAppException("Không tìm thấy đối tượng đối soát.");
        }
    }
}
