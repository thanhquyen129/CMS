using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.FinancialControl;
using LCMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Reconciliations;

/// <summary>
/// Shared reconciliation-detail write path — single line or batch.
/// Creates Variance when unmatched/delta ≠ 0; never opens Exception (Variance ≠ Exception).
/// </summary>
public interface IReconciliationDetailWriter
{
    Task<Guid> AddAsync(
        Reconciliation session,
        Guid tenantId,
        string sourceType,
        Guid sourceId,
        string? targetType,
        Guid? targetId,
        decimal sourceAmount,
        decimal targetAmount,
        decimal matchedAmount,
        string currencyCode,
        string? notes,
        CancellationToken cancellationToken);
}

public sealed class ReconciliationDetailWriter : IReconciliationDetailWriter
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

    private readonly ILcmsDbContext _db;
    private readonly IVarianceSeverityCalculator _severity;

    public ReconciliationDetailWriter(ILcmsDbContext db, IVarianceSeverityCalculator severity)
    {
        _db = db;
        _severity = severity;
    }

    public static void ValidateObjectTypes(string sourceType, string? targetType)
    {
        if (!AllowedObjectTypes.Contains(sourceType.Trim()))
        {
            throw new ConflictAppException("Loại nguồn đối soát không hợp lệ.");
        }

        if (targetType is not null && !AllowedObjectTypes.Contains(targetType.Trim()))
        {
            throw new ConflictAppException("Loại đích đối soát không hợp lệ.");
        }
    }

    public async Task<Guid> AddAsync(
        Reconciliation session,
        Guid tenantId,
        string sourceType,
        Guid sourceId,
        string? targetType,
        Guid? targetId,
        decimal sourceAmount,
        decimal targetAmount,
        decimal matchedAmount,
        string currencyCode,
        string? notes,
        CancellationToken cancellationToken)
    {
        if (string.Equals(session.Status, ReconciliationStatuses.Completed, StringComparison.OrdinalIgnoreCase)
            || string.Equals(session.Status, ReconciliationStatuses.Cancelled, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictAppException("Không thể thêm chi tiết vào phiên đối soát đã hoàn tất hoặc đã hủy.");
        }

        var roundedSource = decimal.Round(sourceAmount, 4, MidpointRounding.AwayFromZero);
        var roundedTarget = decimal.Round(targetAmount, 4, MidpointRounding.AwayFromZero);
        var roundedMatched = decimal.Round(matchedAmount, 4, MidpointRounding.AwayFromZero);
        if (roundedMatched > roundedSource || roundedMatched > roundedTarget)
        {
            throw new ConflictAppException("Số tiền khớp không được vượt số tiền nguồn hoặc đích.");
        }

        var varianceAmount = decimal.Round(roundedSource - roundedMatched, 4, MidpointRounding.AwayFromZero);
        var lineStatus = varianceAmount == 0m && roundedMatched > 0m
            ? ReconciliationDetailStatuses.Matched
            : varianceAmount != 0m
                ? ReconciliationDetailStatuses.Variance
                : ReconciliationDetailStatuses.Unmatched;

        var normalizedSourceType = sourceType.Trim().ToLowerInvariant();
        var normalizedTargetType = string.IsNullOrWhiteSpace(targetType)
            ? null
            : targetType.Trim().ToLowerInvariant();
        var currency = currencyCode.Trim().ToUpperInvariant();

        await EnsureObjectExistsAsync(normalizedSourceType, sourceId, cancellationToken);
        if (targetId.HasValue && normalizedTargetType is not null)
        {
            await EnsureObjectExistsAsync(normalizedTargetType, targetId.Value, cancellationToken);
        }

        var detail = new ReconciliationDetail
        {
            TenantId = tenantId,
            ReconciliationId = session.Id,
            SourceType = normalizedSourceType,
            SourceId = sourceId,
            TargetType = normalizedTargetType,
            TargetId = targetId,
            SourceAmount = roundedSource,
            TargetAmount = roundedTarget,
            MatchedAmount = roundedMatched,
            VarianceAmount = varianceAmount,
            CurrencyCode = currency,
            LineStatus = lineStatus,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()
        };

        _db.ReconciliationDetails.Add(detail);
        await _db.SaveChangesAsync(cancellationToken);

        // Auto-create Variance for unmatched / residual delta (never Exception).
        if (varianceAmount != 0m || lineStatus == ReconciliationDetailStatuses.Unmatched)
        {
            // Fully zero unmatched (source=0,matched=0) still records a zero variance control fact
            // only when line is explicitly unmatched with positive source — already covered by != 0.
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
                    SourceType = normalizedSourceType,
                    SourceId = sourceId,
                    TargetType = normalizedTargetType,
                    TargetId = targetId,
                    Status = VarianceStatuses.Open,
                    Severity = _severity.FromAmount(varianceAmount),
                    Explanation = null,
                    ExceptionId = null
                };
                _db.Variances.Add(variance);
                await _db.SaveChangesAsync(cancellationToken);

                detail.VarianceId = variance.Id;
                await _db.SaveChangesAsync(cancellationToken);
            }
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
