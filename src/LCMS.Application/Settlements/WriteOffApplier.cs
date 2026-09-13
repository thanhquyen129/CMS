using System.Text.Json;
using LCMS.Application.Abstractions;
using LCMS.Application.Audit;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Settlements;

/// <summary>
/// Shared write-off apply path (immediate or after approval) — AdjustmentAmount only; never invents C/R.
/// </summary>
public static class WriteOffApplier
{
    public const string PendingNotesPrefix = "WRITE_OFF_PENDING:";

    public sealed record PendingPayload(decimal Amount, string Reason, string CurrencyCode);

    public static string EncodePendingNotes(PendingPayload payload) =>
        PendingNotesPrefix + JsonSerializer.Serialize(new
        {
            kind = "write_off",
            amount = payload.Amount,
            reason = payload.Reason,
            currency = payload.CurrencyCode
        });

    public static bool TryDecodePendingNotes(string? notes, out PendingPayload payload)
    {
        payload = null!;
        if (string.IsNullOrWhiteSpace(notes) || !notes.StartsWith(PendingNotesPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(notes[PendingNotesPrefix.Length..]);
            var root = doc.RootElement;
            if (!root.TryGetProperty("amount", out var amountEl)
                || !root.TryGetProperty("reason", out var reasonEl))
            {
                return false;
            }

            var amount = amountEl.GetDecimal();
            var reason = reasonEl.GetString() ?? "";
            var currency = root.TryGetProperty("currency", out var c) ? c.GetString() ?? "VND" : "VND";
            if (amount <= 0 || string.IsNullOrWhiteSpace(reason))
            {
                return false;
            }

            payload = new PendingPayload(amount, reason.Trim(), currency);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static async Task ApplyPayableAsync(
        ILcmsDbContext db,
        IAuditWriter audit,
        ICurrentUserContext user,
        AccountsPayable ap,
        decimal amount,
        string reason,
        CancellationToken cancellationToken,
        bool saveChanges = true)
    {
        amount = decimal.Round(amount, 4, MidpointRounding.AwayFromZero);
        reason = reason.Trim();

        if (ap.RecordStatus != "active")
        {
            throw new ConflictAppException("Chỉ được xóa nợ khoản phải trả đang hiệu lực.");
        }

        var outstanding = ap.DeriveOutstanding();
        if (outstanding <= 0)
        {
            throw new ConflictAppException("Khoản phải trả không còn số dư để xóa nợ.");
        }

        if (amount > outstanding)
        {
            throw new ConflictAppException(
                $"Số tiền xóa nợ vượt số dư còn lại ({outstanding}) (C-008).");
        }

        var costCountBefore = await db.Costs.CountAsync(cancellationToken);
        var beforeJson = AuditJson.Serialize(new
        {
            id = ap.Id,
            billId = ap.BillId,
            recognized = ap.RecognizedAmount,
            adjustment = ap.AdjustmentAmount,
            settled = ap.FinalizedSettledAmount,
            outstanding,
            settlementStatus = ap.SettlementStatus,
            currency = ap.CurrencyCode
        });

        var adjBefore = ap.AdjustmentAmount;
        ap.AdjustmentAmount = decimal.Round(ap.AdjustmentAmount - amount, 4, MidpointRounding.AwayFromZero);
        ap.SettlementStatus = SettlementHelpers.DeriveApArSettlementStatus(
            ap.RecognizedAmount, ap.AdjustmentAmount, ap.FinalizedSettledAmount);
        ap.UpdatedAt = DateTimeOffset.UtcNow;
        ap.UpdatedBy = user.UserId;

        var reasonLine = $"[xóa nợ {amount}] {reason}";
        ap.Notes = string.IsNullOrWhiteSpace(ap.Notes)
            ? reasonLine
            : $"{ap.Notes}\n{reasonLine}";

        db.AccountsPayableAdjustments.Add(new AccountsPayableAdjustment
        {
            TenantId = ap.TenantId,
            AccountsPayableId = ap.Id,
            AdjustmentType = ApArAdjustmentTypes.WriteOff,
            DeltaAmount = decimal.Round(-amount, 4, MidpointRounding.AwayFromZero),
            CurrencyCode = ap.CurrencyCode,
            Reason = reason,
            EffectiveDate = DateOnly.FromDateTime(DateTime.UtcNow),
            AdjustmentAmountBefore = adjBefore,
            AdjustmentAmountAfter = ap.AdjustmentAmount,
            OutstandingBefore = outstanding,
            OutstandingAfter = ap.DeriveOutstanding()
        });

        audit.Append(
            AuditActions.AccountsPayableWriteOff,
            AuditObjectTypes.AccountsPayable,
            ap.Id,
            beforeJson: beforeJson,
            afterJson: AuditJson.Serialize(new
            {
                id = ap.Id,
                billId = ap.BillId,
                writeOff = amount,
                recognized = ap.RecognizedAmount,
                adjustment = ap.AdjustmentAmount,
                settled = ap.FinalizedSettledAmount,
                outstanding = ap.DeriveOutstanding(),
                settlementStatus = ap.SettlementStatus,
                currency = ap.CurrencyCode
            }),
            reason: reason);

        if (saveChanges)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        var costCountAfter = await db.Costs.CountAsync(cancellationToken);
        if (saveChanges && costCountAfter != costCountBefore)
        {
            throw new ConflictAppException("Xóa nợ phải trả không được tạo Chi phí mới (C-003).");
        }
    }

    public static async Task ApplyReceivableAsync(
        ILcmsDbContext db,
        IAuditWriter audit,
        ICurrentUserContext user,
        AccountsReceivable ar,
        decimal amount,
        string reason,
        CancellationToken cancellationToken,
        bool saveChanges = true)
    {
        amount = decimal.Round(amount, 4, MidpointRounding.AwayFromZero);
        reason = reason.Trim();

        if (ar.RecordStatus != "active")
        {
            throw new ConflictAppException("Chỉ được xóa nợ khoản phải thu đang hiệu lực.");
        }

        var outstanding = ar.DeriveOutstanding();
        if (outstanding <= 0)
        {
            throw new ConflictAppException("Khoản phải thu không còn số dư để xóa nợ.");
        }

        if (amount > outstanding)
        {
            throw new ConflictAppException(
                $"Số tiền xóa nợ vượt số dư còn lại ({outstanding}) (C-008).");
        }

        var revenueCountBefore = await db.Revenues.CountAsync(cancellationToken);
        var beforeJson = AuditJson.Serialize(new
        {
            id = ar.Id,
            billId = ar.BillId,
            recognized = ar.RecognizedAmount,
            adjustment = ar.AdjustmentAmount,
            settled = ar.FinalizedSettledAmount,
            outstanding,
            settlementStatus = ar.SettlementStatus,
            currency = ar.CurrencyCode
        });

        var adjBefore = ar.AdjustmentAmount;
        ar.AdjustmentAmount = decimal.Round(ar.AdjustmentAmount - amount, 4, MidpointRounding.AwayFromZero);
        ar.SettlementStatus = SettlementHelpers.DeriveApArSettlementStatus(
            ar.RecognizedAmount, ar.AdjustmentAmount, ar.FinalizedSettledAmount);
        ar.UpdatedAt = DateTimeOffset.UtcNow;
        ar.UpdatedBy = user.UserId;

        var reasonLine = $"[xóa nợ {amount}] {reason}";
        ar.Notes = string.IsNullOrWhiteSpace(ar.Notes)
            ? reasonLine
            : $"{ar.Notes}\n{reasonLine}";

        db.AccountsReceivableAdjustments.Add(new AccountsReceivableAdjustment
        {
            TenantId = ar.TenantId,
            AccountsReceivableId = ar.Id,
            AdjustmentType = ApArAdjustmentTypes.WriteOff,
            DeltaAmount = decimal.Round(-amount, 4, MidpointRounding.AwayFromZero),
            CurrencyCode = ar.CurrencyCode,
            Reason = reason,
            EffectiveDate = DateOnly.FromDateTime(DateTime.UtcNow),
            AdjustmentAmountBefore = adjBefore,
            AdjustmentAmountAfter = ar.AdjustmentAmount,
            OutstandingBefore = outstanding,
            OutstandingAfter = ar.DeriveOutstanding()
        });

        audit.Append(
            AuditActions.AccountsReceivableWriteOff,
            AuditObjectTypes.AccountsReceivable,
            ar.Id,
            beforeJson: beforeJson,
            afterJson: AuditJson.Serialize(new
            {
                id = ar.Id,
                billId = ar.BillId,
                writeOff = amount,
                recognized = ar.RecognizedAmount,
                adjustment = ar.AdjustmentAmount,
                settled = ar.FinalizedSettledAmount,
                outstanding = ar.DeriveOutstanding(),
                settlementStatus = ar.SettlementStatus,
                currency = ar.CurrencyCode
            }),
            reason: reason);

        if (saveChanges)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        var revenueCountAfter = await db.Revenues.CountAsync(cancellationToken);
        if (saveChanges && revenueCountAfter != revenueCountBefore)
        {
            throw new ConflictAppException("Xóa nợ phải thu không được tạo Doanh thu mới (C-004).");
        }
    }
}
