using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>
/// Table: payments (D09) — cash-out settlement transaction.
/// Outstanding on AP changes only via finalized payment_allocations (AC-007 / C-008).
/// Never invents Cost (C-003).
/// </summary>
public sealed class Payment : TenantEntityBase
{
    /// <summary>Transaction (cash-out) amount.</summary>
    public decimal Amount { get; set; }

    public string CurrencyCode { get; set; } = "VND";

    /// <summary>Amount in tenant base currency (dated fx_rates or stub — ADR-0004).</summary>
    public decimal? BaseAmount { get; set; }

    /// <summary>Set when converted via persisted fx_rates; null when same-currency or stub fallback.</summary>
    public Guid? FxRateId { get; set; }

    public DateOnly ValueDate { get; set; }
    public Guid? CounterpartyId { get; set; }
    public Guid? BillId { get; set; }
    public string? ReferenceNo { get; set; }
    public string? Notes { get; set; }

    /// <summary>open | cancelled</summary>
    public string Status { get; set; } = PaymentStatuses.Open;

    public string RecordStatus { get; set; } = "active";

    public Bill? Bill { get; set; }
}

public static class PaymentStatuses
{
    public const string Open = "open";
    public const string Cancelled = "cancelled";
}
