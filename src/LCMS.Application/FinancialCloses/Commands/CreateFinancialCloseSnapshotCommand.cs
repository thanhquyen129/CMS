using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Audit;
using LCMS.Application.Common;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.FinancialCloses.Commands;

public sealed record CreateFinancialCloseSnapshotCommand(
    Guid FinancialCloseId,
    string? IdempotencyKey = null) : IRequest<Guid>;

public sealed class CreateFinancialCloseSnapshotCommandValidator : AbstractValidator<CreateFinancialCloseSnapshotCommand>
{
    public CreateFinancialCloseSnapshotCommandValidator()
    {
        RuleFor(x => x.FinancialCloseId).NotEmpty().WithMessage("Lần chốt tài chính không hợp lệ.");
    }
}

/// <summary>
/// Eligibility checklist → create immutable snapshot + details + hash → lock close (C-010 / AC-008).
/// Never mutates prior snapshots; reopen/reclose appends SnapshotVersion.
/// Strict policy forces all eligibility gates (no bypass via config).
/// </summary>
public sealed class CreateFinancialCloseSnapshotCommandHandler
    : IRequestHandler<CreateFinancialCloseSnapshotCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _user;
    private readonly IAuditWriter _audit;
    private readonly ICloseEligibilityChecker _eligibility;
    private readonly IIdempotencyGate _idempotency;

    public CreateFinancialCloseSnapshotCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext user,
        IAuditWriter audit,
        ICloseEligibilityChecker eligibility,
        IIdempotencyGate idempotency)
    {
        _db = db;
        _tenantContext = tenantContext;
        _user = user;
        _audit = audit;
        _eligibility = eligibility;
        _idempotency = idempotency;
    }

    public async Task<Guid> Handle(CreateFinancialCloseSnapshotCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var tenantId = _tenantContext.TenantId!.Value;
        var priorId = await _idempotency.FindAsync(
            IdempotencyScopes.FinancialCloseSnapshot,
            request.IdempotencyKey,
            cancellationToken);
        if (priorId.HasValue)
        {
            return priorId.Value;
        }

        var close = await _db.FinancialCloses
            .FirstOrDefaultAsync(c => c.Id == request.FinancialCloseId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy lần chốt tài chính.");

        if (close.Status == FinancialCloseStatuses.Locked)
        {
            throw new ConflictAppException(
                "Lần chốt đã khóa. Mở lại chốt rồi tạo bản chốt mới — không được sửa bản chốt cũ (C-010).");
        }

        if (close.Status is not (FinancialCloseStatuses.Open or FinancialCloseStatuses.Reopened))
        {
            throw new ConflictAppException("Chỉ được tạo bản chốt khi lần chốt đang mở hoặc đã mở lại.");
        }

        await _eligibility.EnsureEligibleAsync(close, cancellationToken);

        var nextSnapshotVersion = (await _db.FinancialCloseSnapshots
            .Where(s => s.FinancialCloseId == close.Id)
            .Select(s => (int?)s.SnapshotVersion)
            .MaxAsync(cancellationToken) ?? 0) + 1;

        var metrics = await CaptureMetricsAsync(close, cancellationToken);
        var closedAt = DateTimeOffset.UtcNow;
        var hash = ComputeImmutableHash(close.Id, nextSnapshotVersion, close.PolicyVersion, close.BaseCurrency, metrics);

        var snapshot = new FinancialCloseSnapshot
        {
            TenantId = tenantId,
            FinancialCloseId = close.Id,
            ScopeType = close.ScopeType,
            ScopeId = close.ScopeId,
            SnapshotVersion = nextSnapshotVersion,
            ClosedAt = closedAt,
            ClosedBy = _user.UserId,
            PolicyVersion = close.PolicyVersion,
            BaseCurrency = close.BaseCurrency,
            ImmutableHash = hash
        };

        _db.FinancialCloseSnapshots.Add(snapshot);

        var lineNo = 1;
        foreach (var m in metrics)
        {
            _db.FinancialCloseSnapshotDetails.Add(new FinancialCloseSnapshotDetail
            {
                TenantId = tenantId,
                SnapshotId = snapshot.Id,
                LineNo = lineNo++,
                MetricKey = m.Key,
                MetricValue = m.Value,
                CurrencyCode = m.CurrencyCode,
                SourceType = m.SourceType,
                SourceId = m.SourceId,
                Notes = m.Notes
            });
        }

        close.Status = FinancialCloseStatuses.Locked;
        close.LockedAt = closedAt;
        close.LockedBy = _user.UserId;

        _audit.Append(
            AuditActions.FinancialCloseSnapshotCreate,
            AuditObjectTypes.FinancialCloseSnapshot,
            snapshot.Id,
            afterJson: AuditJson.Serialize(new
            {
                snapshotId = snapshot.Id,
                financialCloseId = close.Id,
                snapshotVersion = nextSnapshotVersion,
                immutableHash = hash,
                policyVersion = close.PolicyVersion,
                scopeType = close.ScopeType,
                scopeId = close.ScopeId,
                periodFrom = close.PeriodFrom,
                periodTo = close.PeriodTo,
                metricCount = metrics.Count,
                closedAt
            }));

        _idempotency.Remember(
            IdempotencyScopes.FinancialCloseSnapshot,
            request.IdempotencyKey ?? string.Empty,
            snapshot.Id,
            tenantId);
        await _db.SaveChangesAsync(cancellationToken);
        return snapshot.Id;
    }

    private async Task<List<SnapshotMetric>> CaptureMetricsAsync(
        FinancialClose close,
        CancellationToken cancellationToken)
    {
        var costs = _db.Costs.AsNoTracking().AsQueryable();
        var revenues = _db.Revenues.AsNoTracking().AsQueryable();
        var ap = _db.AccountsPayable.AsNoTracking().AsQueryable();
        var ar = _db.AccountsReceivable.AsNoTracking().AsQueryable();

        if (close.ScopeType == FinancialCloseScopeTypes.Bill && close.ScopeId.HasValue)
        {
            var billId = close.ScopeId.Value;
            costs = costs.Where(c => c.BillId == billId);
            revenues = revenues.Where(r => r.BillId == billId);
            ap = ap.Where(a => a.BillId == billId);
            ar = ar.Where(a => a.BillId == billId);
        }

        var costList = await costs.ToListAsync(cancellationToken);
        var revenueList = await revenues.ToListAsync(cancellationToken);
        var apList = await ap.ToListAsync(cancellationToken);
        var arList = await ar.ToListAsync(cancellationToken);

        var waivedQuery = _db.Exceptions.AsNoTracking()
            .Where(e => e.Status == ExceptionStatuses.Waived);
        if (close.ScopeType == FinancialCloseScopeTypes.Bill && close.ScopeId.HasValue)
        {
            var billId = close.ScopeId.Value;
            waivedQuery = waivedQuery.Where(e => e.BillId == billId);
        }

        var waived = await waivedQuery.ToListAsync(cancellationToken);

        var currency = close.BaseCurrency;
        var metrics = new List<SnapshotMetric>
        {
            new("cost_count", costList.Count, null, "cost", null, null),
            new("cost_total", costList.Where(c => c.CurrencyCode == currency).Sum(c => c.Amount), currency, "cost", null, null),
            new("revenue_count", revenueList.Count, null, "revenue", null, null),
            new("revenue_total", revenueList.Where(r => r.CurrencyCode == currency).Sum(r => r.Amount), currency, "revenue", null, null),
            new("ap_count", apList.Count, null, "accounts_payable", null, null),
            new("ap_outstanding_total", apList.Where(a => a.CurrencyCode == currency).Sum(a => a.DeriveOutstanding()), currency, "accounts_payable", null, null),
            new("ar_count", arList.Count, null, "accounts_receivable", null, null),
            new("ar_outstanding_total", arList.Where(a => a.CurrencyCode == currency).Sum(a => a.DeriveOutstanding()), currency, "accounts_receivable", null, null),
            new("waiver_count", waived.Count, null, "exception", null, null)
        };

        foreach (var w in waived.OrderBy(e => e.Id))
        {
            metrics.Add(new SnapshotMetric(
                "waiver",
                1m,
                null,
                "exception",
                w.Id,
                string.IsNullOrWhiteSpace(w.ResolutionNotes) ? w.Title : $"{w.Title}: {w.ResolutionNotes}"));
        }

        return metrics;
    }

    private static string ComputeImmutableHash(
        Guid closeId,
        int snapshotVersion,
        string policyVersion,
        string baseCurrency,
        IReadOnlyList<SnapshotMetric> metrics)
    {
        var sb = new StringBuilder();
        sb.Append(closeId).Append('|')
            .Append(snapshotVersion).Append('|')
            .Append(policyVersion).Append('|')
            .Append(baseCurrency).Append('|');
        foreach (var m in metrics.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            sb.Append(m.Key).Append('=')
                .Append(m.Value.ToString("0.####"))
                .Append(';')
                .Append(m.CurrencyCode ?? "")
                .Append('|');
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private sealed record SnapshotMetric(
        string Key,
        decimal Value,
        string? CurrencyCode,
        string? SourceType,
        Guid? SourceId,
        string? Notes);
}
