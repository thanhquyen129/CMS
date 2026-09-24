using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Common;

/// <summary>Optional Idempotency-Key gate: same scope+key returns the first object id.</summary>
public interface IIdempotencyGate
{
    Task<Guid?> FindAsync(string scope, string? key, CancellationToken cancellationToken);

    void Remember(string scope, string key, Guid objectId, Guid tenantId);
}

public sealed class IdempotencyGate : IIdempotencyGate
{
    private readonly ILcmsDbContext _db;

    public IdempotencyGate(ILcmsDbContext db) => _db = db;

    public async Task<Guid?> FindAsync(string scope, string? key, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var normalized = key.Trim();
        var prior = await _db.IdempotencyRecords.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Scope == scope && r.Key == normalized, cancellationToken);
        return prior?.ObjectId;
    }

    public void Remember(string scope, string key, Guid objectId, Guid tenantId)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        _db.IdempotencyRecords.Add(new IdempotencyRecord
        {
            TenantId = tenantId,
            Scope = scope,
            Key = key.Trim(),
            ObjectId = objectId
        });
    }
}

public static class IdempotencyScopes
{
    public const string FinancialDocument = "financial_document";
    public const string Payment = "payment";
    public const string Collection = "collection";
    public const string Cost = "cost";
    public const string Revenue = "revenue";
    public const string DocumentMatch = "document_match";
    public const string RecognizePayable = "recognize_payable";
    public const string RecognizeReceivable = "recognize_receivable";
    public const string FinancialClose = "financial_close";
    public const string FinancialCloseSnapshot = "financial_close_snapshot";
    public const string CostAdjustment = "cost_adjustment";
    public const string RevenueAdjustment = "revenue_adjustment";
    public const string CostAllocation = "cost_allocation";
    public const string PaymentAllocation = "payment_allocation";
    public const string CollectionAllocation = "collection_allocation";
    public const string CostConfirm = "cost_confirm";
    public const string CostActualize = "cost_actualize";
    public const string RevenueConfirm = "revenue_confirm";
    public const string RevenueActualize = "revenue_actualize";
    public const string ApAdjustment = "ap_adjustment";
    public const string ArAdjustment = "ar_adjustment";
}

public static class IdempotencyReplay
{
    public static async Task<bool> AlreadyAppliedAsync(
        IIdempotencyGate gate,
        string scope,
        string? key,
        Guid objectId,
        CancellationToken cancellationToken)
    {
        var prior = await gate.FindAsync(scope, key, cancellationToken);
        if (!prior.HasValue)
        {
            return false;
        }

        if (prior.Value != objectId)
        {
            throw new ConflictAppException("Khóa idempotency đã dùng cho bản ghi khác.");
        }

        return true;
    }
}
