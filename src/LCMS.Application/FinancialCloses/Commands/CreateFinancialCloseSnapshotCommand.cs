using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.FinancialCloses.Commands;

public sealed record CreateFinancialCloseSnapshotCommand(Guid FinancialCloseId) : IRequest<Guid>;

public sealed class CreateFinancialCloseSnapshotCommandValidator : AbstractValidator<CreateFinancialCloseSnapshotCommand>
{
    public CreateFinancialCloseSnapshotCommandValidator()
    {
        RuleFor(x => x.FinancialCloseId).NotEmpty().WithMessage("Lần chốt tài chính không hợp lệ.");
    }
}

/// <summary>
/// Eligibility stub → create immutable snapshot + details + hash → lock close (C-010 / AC-008).
/// Never mutates prior snapshots; reopen/reclose appends SnapshotVersion.
/// </summary>
public sealed class CreateFinancialCloseSnapshotCommandHandler
    : IRequestHandler<CreateFinancialCloseSnapshotCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _user;

    public CreateFinancialCloseSnapshotCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext user)
    {
        _db = db;
        _tenantContext = tenantContext;
        _user = user;
    }

    public async Task<Guid> Handle(CreateFinancialCloseSnapshotCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var tenantId = _tenantContext.TenantId!.Value;
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

        await EnsureEligibleAsync(close, cancellationToken);

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
                Notes = m.Notes
            });
        }

        close.Status = FinancialCloseStatuses.Locked;
        close.LockedAt = closedAt;
        close.LockedBy = _user.UserId;

        await _db.SaveChangesAsync(cancellationToken);
        return snapshot.Id;
    }

    private async Task EnsureEligibleAsync(FinancialClose close, CancellationToken cancellationToken)
    {
        // Thin eligibility: block when open/in_progress critical exceptions exist in scope.
        var query = _db.Exceptions.AsNoTracking()
            .Where(e =>
                (e.Status == ExceptionStatuses.Open || e.Status == ExceptionStatuses.InProgress)
                && e.Severity == ExceptionSeverities.Critical);

        if (close.ScopeType == FinancialCloseScopeTypes.Bill && close.ScopeId.HasValue)
        {
            query = query.Where(e => e.BillId == null || e.BillId == close.ScopeId);
        }

        if (await query.AnyAsync(cancellationToken))
        {
            throw new ConflictAppException(
                "Không đủ điều kiện chốt: còn ngoại lệ mức nghiêm trọng đang mở.");
        }
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

        var currency = close.BaseCurrency;
        return
        [
            new SnapshotMetric("cost_count", costList.Count, null, "cost", null),
            new SnapshotMetric("cost_total", costList.Where(c => c.CurrencyCode == currency).Sum(c => c.Amount), currency, "cost", null),
            new SnapshotMetric("revenue_count", revenueList.Count, null, "revenue", null),
            new SnapshotMetric("revenue_total", revenueList.Where(r => r.CurrencyCode == currency).Sum(r => r.Amount), currency, "revenue", null),
            new SnapshotMetric("ap_count", apList.Count, null, "accounts_payable", null),
            new SnapshotMetric("ap_outstanding_total", apList.Where(a => a.CurrencyCode == currency).Sum(a => a.DeriveOutstanding()), currency, "accounts_payable", null),
            new SnapshotMetric("ar_count", arList.Count, null, "accounts_receivable", null),
            new SnapshotMetric("ar_outstanding_total", arList.Where(a => a.CurrencyCode == currency).Sum(a => a.DeriveOutstanding()), currency, "accounts_receivable", null)
        ];
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
        string? Notes);
}
