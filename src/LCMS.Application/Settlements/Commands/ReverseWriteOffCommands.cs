using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Audit;
using LCMS.Application.Common;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Settlements.Commands;

/// <summary>
/// Hoàn tác xóa nợ phải thu: compensating write_off_reversal row linked to the original write-off.
/// Original row is never edited or deleted; Revenue is untouched (C-004).
/// </summary>
public sealed record ReverseWriteOffAccountsReceivableCommand(
    Guid AccountsReceivableId,
    Guid WriteOffAdjustmentId,
    string Reason,
    string? IfMatch = null) : IRequest<Guid>;

/// <summary>Hoàn tác xóa nợ phải trả — symmetric to AR; Cost is untouched (C-003).</summary>
public sealed record ReverseWriteOffAccountsPayableCommand(
    Guid AccountsPayableId,
    Guid WriteOffAdjustmentId,
    string Reason,
    string? IfMatch = null) : IRequest<Guid>;

public sealed class ReverseWriteOffAccountsReceivableCommandValidator
    : AbstractValidator<ReverseWriteOffAccountsReceivableCommand>
{
    public ReverseWriteOffAccountsReceivableCommandValidator()
    {
        RuleFor(x => x.AccountsReceivableId).NotEmpty().WithMessage("Khoản phải thu không hợp lệ.");
        RuleFor(x => x.WriteOffAdjustmentId).NotEmpty().WithMessage("Bút toán xóa nợ không hợp lệ.");
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Lý do hoàn tác xóa nợ không được để trống.")
            .MaximumLength(1024).WithMessage("Lý do hoàn tác xóa nợ không được vượt quá 1024 ký tự.");
    }
}

public sealed class ReverseWriteOffAccountsPayableCommandValidator
    : AbstractValidator<ReverseWriteOffAccountsPayableCommand>
{
    public ReverseWriteOffAccountsPayableCommandValidator()
    {
        RuleFor(x => x.AccountsPayableId).NotEmpty().WithMessage("Khoản phải trả không hợp lệ.");
        RuleFor(x => x.WriteOffAdjustmentId).NotEmpty().WithMessage("Bút toán xóa nợ không hợp lệ.");
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Lý do hoàn tác xóa nợ không được để trống.")
            .MaximumLength(1024).WithMessage("Lý do hoàn tác xóa nợ không được vượt quá 1024 ký tự.");
    }
}

public sealed class ReverseWriteOffAccountsReceivableCommandHandler
    : IRequestHandler<ReverseWriteOffAccountsReceivableCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _user;
    private readonly IAuditWriter _audit;
    private readonly IPermissionService _permissions;
    private readonly IRowVersionGuard _versions;

    public ReverseWriteOffAccountsReceivableCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext user,
        IAuditWriter audit,
        IPermissionService permissions,
        IRowVersionGuard versions)
    {
        _db = db;
        _tenantContext = tenantContext;
        _user = user;
        _audit = audit;
        _permissions = permissions;
        _versions = versions;
    }

    public async Task<Guid> Handle(
        ReverseWriteOffAccountsReceivableCommand request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        await _permissions.EnsureAsync(
            PermissionCodes.ArWriteOff,
            "Bạn không có quyền hoàn tác xóa nợ phải thu.",
            cancellationToken);

        var ar = await _db.AccountsReceivable
            .FirstOrDefaultAsync(a => a.Id == request.AccountsReceivableId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy khoản phải thu.");
        _versions.EnsureCurrent(ar, request.IfMatch);

        var original = await _db.AccountsReceivableAdjustments.AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == request.WriteOffAdjustmentId && x.AccountsReceivableId == ar.Id,
                cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy bút toán xóa nợ trên khoản phải thu này.");

        if (original.AdjustmentType != ApArAdjustmentTypes.WriteOff)
        {
            throw new ConflictAppException("Chỉ hoàn tác được bút toán xóa nợ.");
        }

        if (ar.RecordStatus != ApArRecordStatuses.Active)
        {
            throw new ConflictAppException("Khoản phải thu đã hủy ghi nhận — không hoàn tác xóa nợ.");
        }

        var alreadyReversed = await _db.AccountsReceivableAdjustments.AsNoTracking()
            .AnyAsync(x => x.ReversesAdjustmentId == original.Id, cancellationToken);
        if (alreadyReversed)
        {
            throw new ConflictAppException("Bút toán xóa nợ này đã được hoàn tác.");
        }

        var restore = decimal.Round(-original.DeltaAmount, 4, MidpointRounding.AwayFromZero);
        var reason = request.Reason.Trim();
        var revenueCountBefore = await _db.Revenues.CountAsync(cancellationToken);

        var outstandingBefore = ar.DeriveOutstanding();
        var adjBefore = ar.AdjustmentAmount;
        var statusBefore = ar.SettlementStatus;
        ar.AdjustmentAmount = decimal.Round(ar.AdjustmentAmount + restore, 4, MidpointRounding.AwayFromZero);
        ar.SettlementStatus = SettlementHelpers.DeriveApArSettlementStatus(
            ar.RecognizedAmount, ar.AdjustmentAmount, ar.FinalizedSettledAmount);
        ar.UpdatedAt = DateTimeOffset.UtcNow;
        ar.UpdatedBy = _user.UserId;
        var reasonLine = $"[hoàn tác xóa nợ {restore}] {reason}";
        ar.Notes = string.IsNullOrWhiteSpace(ar.Notes) ? reasonLine : $"{ar.Notes}\n{reasonLine}";

        var row = new AccountsReceivableAdjustment
        {
            TenantId = ar.TenantId,
            AccountsReceivableId = ar.Id,
            AdjustmentType = ApArAdjustmentTypes.WriteOffReversal,
            DeltaAmount = restore,
            CurrencyCode = ar.CurrencyCode,
            Reason = reason,
            EffectiveDate = DateOnly.FromDateTime(DateTime.UtcNow),
            AdjustmentAmountBefore = adjBefore,
            AdjustmentAmountAfter = ar.AdjustmentAmount,
            OutstandingBefore = outstandingBefore,
            OutstandingAfter = ar.DeriveOutstanding(),
            ReversesAdjustmentId = original.Id
        };
        _db.AccountsReceivableAdjustments.Add(row);

        _audit.Append(
            AuditActions.AccountsReceivableWriteOffReverse,
            AuditObjectTypes.AccountsReceivable,
            ar.Id,
            beforeJson: AuditJson.Serialize(new
            {
                id = ar.Id,
                billId = ar.BillId,
                adjustment = adjBefore,
                outstanding = outstandingBefore,
                settlementStatus = statusBefore,
                currency = ar.CurrencyCode
            }),
            afterJson: AuditJson.Serialize(new
            {
                id = ar.Id,
                billId = ar.BillId,
                adjustmentId = row.Id,
                reversesAdjustmentId = original.Id,
                restored = restore,
                adjustment = ar.AdjustmentAmount,
                outstanding = row.OutstandingAfter,
                settlementStatus = ar.SettlementStatus,
                currency = ar.CurrencyCode
            }),
            reason: reason);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new ConflictAppException("Bút toán xóa nợ này đã được hoàn tác.");
        }

        if (await _db.Revenues.CountAsync(cancellationToken) != revenueCountBefore)
        {
            throw new ConflictAppException("Hoàn tác xóa nợ không được tạo Doanh thu mới (C-004).");
        }

        return row.Id;
    }
}

public sealed class ReverseWriteOffAccountsPayableCommandHandler
    : IRequestHandler<ReverseWriteOffAccountsPayableCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _user;
    private readonly IAuditWriter _audit;
    private readonly IPermissionService _permissions;
    private readonly IRowVersionGuard _versions;

    public ReverseWriteOffAccountsPayableCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext user,
        IAuditWriter audit,
        IPermissionService permissions,
        IRowVersionGuard versions)
    {
        _db = db;
        _tenantContext = tenantContext;
        _user = user;
        _audit = audit;
        _permissions = permissions;
        _versions = versions;
    }

    public async Task<Guid> Handle(
        ReverseWriteOffAccountsPayableCommand request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        await _permissions.EnsureAsync(
            PermissionCodes.ApWriteOff,
            "Bạn không có quyền hoàn tác xóa nợ phải trả.",
            cancellationToken);

        var ap = await _db.AccountsPayable
            .FirstOrDefaultAsync(a => a.Id == request.AccountsPayableId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy khoản phải trả.");
        _versions.EnsureCurrent(ap, request.IfMatch);

        var original = await _db.AccountsPayableAdjustments.AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == request.WriteOffAdjustmentId && x.AccountsPayableId == ap.Id,
                cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy bút toán xóa nợ trên khoản phải trả này.");

        if (original.AdjustmentType != ApArAdjustmentTypes.WriteOff)
        {
            throw new ConflictAppException("Chỉ hoàn tác được bút toán xóa nợ.");
        }

        if (ap.RecordStatus != ApArRecordStatuses.Active)
        {
            throw new ConflictAppException("Khoản phải trả đã hủy ghi nhận — không hoàn tác xóa nợ.");
        }

        var alreadyReversed = await _db.AccountsPayableAdjustments.AsNoTracking()
            .AnyAsync(x => x.ReversesAdjustmentId == original.Id, cancellationToken);
        if (alreadyReversed)
        {
            throw new ConflictAppException("Bút toán xóa nợ này đã được hoàn tác.");
        }

        var restore = decimal.Round(-original.DeltaAmount, 4, MidpointRounding.AwayFromZero);
        var reason = request.Reason.Trim();
        var costCountBefore = await _db.Costs.CountAsync(cancellationToken);

        var outstandingBefore = ap.DeriveOutstanding();
        var adjBefore = ap.AdjustmentAmount;
        var statusBefore = ap.SettlementStatus;
        ap.AdjustmentAmount = decimal.Round(ap.AdjustmentAmount + restore, 4, MidpointRounding.AwayFromZero);
        ap.SettlementStatus = SettlementHelpers.DeriveApArSettlementStatus(
            ap.RecognizedAmount, ap.AdjustmentAmount, ap.FinalizedSettledAmount);
        ap.UpdatedAt = DateTimeOffset.UtcNow;
        ap.UpdatedBy = _user.UserId;
        var reasonLine = $"[hoàn tác xóa nợ {restore}] {reason}";
        ap.Notes = string.IsNullOrWhiteSpace(ap.Notes) ? reasonLine : $"{ap.Notes}\n{reasonLine}";

        var row = new AccountsPayableAdjustment
        {
            TenantId = ap.TenantId,
            AccountsPayableId = ap.Id,
            AdjustmentType = ApArAdjustmentTypes.WriteOffReversal,
            DeltaAmount = restore,
            CurrencyCode = ap.CurrencyCode,
            Reason = reason,
            EffectiveDate = DateOnly.FromDateTime(DateTime.UtcNow),
            AdjustmentAmountBefore = adjBefore,
            AdjustmentAmountAfter = ap.AdjustmentAmount,
            OutstandingBefore = outstandingBefore,
            OutstandingAfter = ap.DeriveOutstanding(),
            ReversesAdjustmentId = original.Id
        };
        _db.AccountsPayableAdjustments.Add(row);

        _audit.Append(
            AuditActions.AccountsPayableWriteOffReverse,
            AuditObjectTypes.AccountsPayable,
            ap.Id,
            beforeJson: AuditJson.Serialize(new
            {
                id = ap.Id,
                billId = ap.BillId,
                adjustment = adjBefore,
                outstanding = outstandingBefore,
                settlementStatus = statusBefore,
                currency = ap.CurrencyCode
            }),
            afterJson: AuditJson.Serialize(new
            {
                id = ap.Id,
                billId = ap.BillId,
                adjustmentId = row.Id,
                reversesAdjustmentId = original.Id,
                restored = restore,
                adjustment = ap.AdjustmentAmount,
                outstanding = row.OutstandingAfter,
                settlementStatus = ap.SettlementStatus,
                currency = ap.CurrencyCode
            }),
            reason: reason);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new ConflictAppException("Bút toán xóa nợ này đã được hoàn tác.");
        }

        if (await _db.Costs.CountAsync(cancellationToken) != costCountBefore)
        {
            throw new ConflictAppException("Hoàn tác xóa nợ không được tạo Chi phí mới (C-003).");
        }

        return row.Id;
    }
}
