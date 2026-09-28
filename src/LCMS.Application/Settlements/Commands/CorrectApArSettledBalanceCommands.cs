using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Audit;
using LCMS.Application.Common;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.Exposures.Queries;
using LCMS.Application.Identity;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Settlements.Commands;

/// <summary>
/// FIN-DATA-01 / ADR-0038: re-align AR FinalizedSettledAmount with the finalized, non-reversed allocations
/// when a pre-fix cross-currency allocation reversal released the cash amount instead of the settled amount.
/// Source rows (allocations, adjustments) are never edited; the change is audited with before/after and reason.
/// </summary>
public sealed record CorrectAccountsReceivableSettledBalanceCommand(
    Guid AccountsReceivableId,
    string Reason,
    string? IfMatch = null) : IRequest<ApArSettledCorrectionResult>;

/// <summary>AP counterpart of <see cref="CorrectAccountsReceivableSettledBalanceCommand"/>.</summary>
public sealed record CorrectAccountsPayableSettledBalanceCommand(
    Guid AccountsPayableId,
    string Reason,
    string? IfMatch = null) : IRequest<ApArSettledCorrectionResult>;

public sealed record ApArSettledCorrectionResult(
    Guid AccountId,
    decimal OutstandingBefore,
    decimal OutstandingAfter,
    decimal SettledBefore,
    decimal SettledAfter);

public sealed class CorrectAccountsReceivableSettledBalanceCommandValidator
    : AbstractValidator<CorrectAccountsReceivableSettledBalanceCommand>
{
    public CorrectAccountsReceivableSettledBalanceCommandValidator()
    {
        RuleFor(x => x.AccountsReceivableId).NotEmpty().WithMessage("Khoản phải thu không hợp lệ.");
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Lý do đối soát không được để trống.")
            .MaximumLength(1024).WithMessage("Lý do đối soát không được vượt quá 1024 ký tự.");
    }
}

public sealed class CorrectAccountsPayableSettledBalanceCommandValidator
    : AbstractValidator<CorrectAccountsPayableSettledBalanceCommand>
{
    public CorrectAccountsPayableSettledBalanceCommandValidator()
    {
        RuleFor(x => x.AccountsPayableId).NotEmpty().WithMessage("Khoản phải trả không hợp lệ.");
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Lý do đối soát không được để trống.")
            .MaximumLength(1024).WithMessage("Lý do đối soát không được vượt quá 1024 ký tự.");
    }
}

public sealed class CorrectApArSettledBalanceCommandHandler
    : IRequestHandler<CorrectAccountsReceivableSettledBalanceCommand, ApArSettledCorrectionResult>,
      IRequestHandler<CorrectAccountsPayableSettledBalanceCommand, ApArSettledCorrectionResult>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _user;
    private readonly IPermissionService _permissions;
    private readonly IOrganizationHierarchyService _orgHierarchy;
    private readonly IRowVersionGuard _versions;
    private readonly IAuditWriter _audit;

    public CorrectApArSettledBalanceCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext user,
        IPermissionService permissions,
        IOrganizationHierarchyService orgHierarchy,
        IRowVersionGuard versions,
        IAuditWriter audit)
    {
        _db = db;
        _tenantContext = tenantContext;
        _user = user;
        _permissions = permissions;
        _orgHierarchy = orgHierarchy;
        _versions = versions;
        _audit = audit;
    }

    public async Task<ApArSettledCorrectionResult> Handle(
        CorrectAccountsReceivableSettledBalanceCommand request,
        CancellationToken cancellationToken)
    {
        await EnsureAccessAsync(cancellationToken);
        var scoped = await ApArLedgerAccess.LoadReceivableAsync(
            _db, _user, _permissions, _orgHierarchy, request.AccountsReceivableId, cancellationToken);
        var ar = await _db.AccountsReceivable.FirstAsync(a => a.Id == scoped.Id, cancellationToken);
        _versions.EnsureCurrent(ar, request.IfMatch);

        var deltas = await _db.AccountsReceivableAdjustments.AsNoTracking()
            .Where(x => x.AccountsReceivableId == ar.Id)
            .Select(x => x.DeltaAmount)
            .ToListAsync(cancellationToken);
        var allocations = await _db.CollectionAllocations.AsNoTracking()
            .Where(a => a.AccountsReceivableId == ar.Id && a.FinalizedAt != null)
            .Select(a => new ApArBalanceAnalyzer.AllocationSnapshot(
                a.Id, a.CollectionId, a.Amount, a.CurrencyCode, a.SettledAmount, a.FinalizedAt, a.ReversedAt))
            .ToListAsync(cancellationToken);
        var result = ApArBalanceAnalyzer.Analyze(
            new(ar.RecognizedAmount, ar.AdjustmentAmount, ar.FinalizedSettledAmount, ar.CurrencyCode),
            deltas,
            allocations);
        EnsureCorrectable(result);

        var reason = request.Reason.Trim();
        var outstandingBefore = ar.DeriveOutstanding();
        var settledBefore = ar.FinalizedSettledAmount;
        var statusBefore = ar.SettlementStatus;
        ar.FinalizedSettledAmount = result.DerivedSettledAmount;
        ar.SettlementStatus = SettlementHelpers.DeriveApArSettlementStatus(
            ar.RecognizedAmount, ar.AdjustmentAmount, ar.FinalizedSettledAmount);
        ar.UpdatedAt = DateTimeOffset.UtcNow;
        ar.UpdatedBy = _user.UserId;
        var line = $"[đối soát số dư {settledBefore} → {ar.FinalizedSettledAmount}] {reason}";
        ar.Notes = string.IsNullOrWhiteSpace(ar.Notes) ? line : $"{ar.Notes}\n{line}";

        AppendAudit(
            AuditActions.AccountsReceivableSettlementCorrection,
            AuditObjectTypes.AccountsReceivable,
            ar.Id, ar.BillId, ar.CurrencyCode,
            outstandingBefore, ar.DeriveOutstanding(),
            settledBefore, ar.FinalizedSettledAmount,
            statusBefore, ar.SettlementStatus,
            result, reason);

        await SaveAsync(cancellationToken);
        return new ApArSettledCorrectionResult(
            ar.Id, outstandingBefore, ar.DeriveOutstanding(), settledBefore, ar.FinalizedSettledAmount);
    }

    public async Task<ApArSettledCorrectionResult> Handle(
        CorrectAccountsPayableSettledBalanceCommand request,
        CancellationToken cancellationToken)
    {
        await EnsureAccessAsync(cancellationToken);
        var scoped = await ApArLedgerAccess.LoadPayableAsync(
            _db, _user, _permissions, _orgHierarchy, request.AccountsPayableId, cancellationToken);
        var ap = await _db.AccountsPayable.FirstAsync(a => a.Id == scoped.Id, cancellationToken);
        _versions.EnsureCurrent(ap, request.IfMatch);

        var deltas = await _db.AccountsPayableAdjustments.AsNoTracking()
            .Where(x => x.AccountsPayableId == ap.Id)
            .Select(x => x.DeltaAmount)
            .ToListAsync(cancellationToken);
        var allocations = await _db.PaymentAllocations.AsNoTracking()
            .Where(a => a.AccountsPayableId == ap.Id && a.FinalizedAt != null)
            .Select(a => new ApArBalanceAnalyzer.AllocationSnapshot(
                a.Id, a.PaymentId, a.Amount, a.CurrencyCode, a.SettledAmount, a.FinalizedAt, a.ReversedAt))
            .ToListAsync(cancellationToken);
        var result = ApArBalanceAnalyzer.Analyze(
            new(ap.RecognizedAmount, ap.AdjustmentAmount, ap.FinalizedSettledAmount, ap.CurrencyCode),
            deltas,
            allocations);
        EnsureCorrectable(result);

        var reason = request.Reason.Trim();
        var outstandingBefore = ap.DeriveOutstanding();
        var settledBefore = ap.FinalizedSettledAmount;
        var statusBefore = ap.SettlementStatus;
        ap.FinalizedSettledAmount = result.DerivedSettledAmount;
        ap.SettlementStatus = SettlementHelpers.DeriveApArSettlementStatus(
            ap.RecognizedAmount, ap.AdjustmentAmount, ap.FinalizedSettledAmount);
        ap.UpdatedAt = DateTimeOffset.UtcNow;
        ap.UpdatedBy = _user.UserId;
        var line = $"[đối soát số dư {settledBefore} → {ap.FinalizedSettledAmount}] {reason}";
        ap.Notes = string.IsNullOrWhiteSpace(ap.Notes) ? line : $"{ap.Notes}\n{line}";

        AppendAudit(
            AuditActions.AccountsPayableSettlementCorrection,
            AuditObjectTypes.AccountsPayable,
            ap.Id, ap.BillId, ap.CurrencyCode,
            outstandingBefore, ap.DeriveOutstanding(),
            settledBefore, ap.FinalizedSettledAmount,
            statusBefore, ap.SettlementStatus,
            result, reason);

        await SaveAsync(cancellationToken);
        return new ApArSettledCorrectionResult(
            ap.Id, outstandingBefore, ap.DeriveOutstanding(), settledBefore, ap.FinalizedSettledAmount);
    }

    private async Task EnsureAccessAsync(CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        await _permissions.EnsureAsync(
            PermissionCodes.ApArReconcile,
            "Bạn không có quyền đối soát số dư công nợ.",
            cancellationToken);
    }

    private static void EnsureCorrectable(ApArBalanceAnalyzer.Result result)
    {
        if (result.Reconciled)
        {
            throw new ConflictAppException("Số dư đã khớp với sổ công nợ — không cần đối soát.");
        }

        if (!result.Correctable)
        {
            throw new ConflictAppException(
                "Chênh lệch không do hủy phân bổ khác tiền tệ trước bản sửa — cần kiểm tra thủ công, không tự điều chỉnh.");
        }
    }

    private void AppendAudit(
        string action,
        string objectType,
        Guid accountId,
        Guid? billId,
        string currency,
        decimal outstandingBefore,
        decimal outstandingAfter,
        decimal settledBefore,
        decimal settledAfter,
        string statusBefore,
        string statusAfter,
        ApArBalanceAnalyzer.Result result,
        string reason)
    {
        _audit.Append(
            action,
            objectType,
            accountId,
            beforeJson: AuditJson.Serialize(new
            {
                id = accountId,
                billId,
                finalizedSettled = settledBefore,
                outstanding = outstandingBefore,
                settlementStatus = statusBefore,
                currency
            }),
            afterJson: AuditJson.Serialize(new
            {
                id = accountId,
                billId,
                finalizedSettled = settledAfter,
                outstanding = outstandingAfter,
                settlementStatus = statusAfter,
                ledgerBalance = result.LedgerBalance,
                currency,
                cause = ApArBalanceAnalyzer.CauseLegacyCrossCurrencyReversal,
                legacyAllocations = result.LegacyAllocations.Select(l => new
                {
                    allocationId = l.AllocationId,
                    cashId = l.CashId,
                    cashAmount = l.CashAmount,
                    cashCurrency = l.CashCurrencyCode,
                    settledAmount = l.SettledAmount,
                    reversedAt = l.ReversedAt
                })
            }),
            reason: reason);
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictAppException("Dữ liệu công nợ vừa thay đổi. Tải lại trang rồi đối soát lại.");
        }
    }
}
