using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.BusinessParties;

/// <summary>ADR-0020 — transactability, role gate, and AR credit ceiling.</summary>
public interface IPartyDirectoryService
{
    Task EnsureUsableAsync(
        Guid partyId,
        IReadOnlyList<string> requiredAnyRoles,
        string purposeVi,
        CancellationToken cancellationToken);

    Task EnsureCreditAllowsArRecognizeAsync(
        Guid? partyId,
        decimal additionalAmount,
        string currencyCode,
        CancellationToken cancellationToken);

    Task<PartyCreditEvaluation> EvaluateCreditAsync(
        Guid partyId,
        decimal additionalAmount,
        string? currencyCode,
        CancellationToken cancellationToken);

    Task<(decimal ApOutstanding, decimal ArOutstanding, int OpenApCount, int OpenArCount)> GetOpenExposureTotalsAsync(
        Guid partyId,
        CancellationToken cancellationToken);
}

public sealed record PartyCreditEvaluation(
    string Mode,
    decimal? CreditLimit,
    string? CreditLimitCurrencyCode,
    decimal ArOutstandingSameCurrency,
    decimal? UtilizationPercent,
    string Status,
    bool WouldBlock,
    string? Message);

public sealed class PartyDirectoryService : IPartyDirectoryService
{
    public const decimal WatchRatio = 0.80m;

    private readonly ILcmsDbContext _db;

    public PartyDirectoryService(ILcmsDbContext db)
    {
        _db = db;
    }

    public async Task EnsureUsableAsync(
        Guid partyId,
        IReadOnlyList<string> requiredAnyRoles,
        string purposeVi,
        CancellationToken cancellationToken)
    {
        var party = await _db.BusinessParties.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == partyId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy đối tác.");

        if (party.IsBlocked)
        {
            var reason = string.IsNullOrWhiteSpace(party.BlockedReason)
                ? "."
                : $": {party.BlockedReason}";
            throw new ConflictAppException($"Đối tác {party.Code} đang bị chặn giao dịch{reason}");
        }

        if (!party.IsActive)
        {
            throw new ConflictAppException($"Đối tác {party.Code} đã ngừng dùng.");
        }

        if (requiredAnyRoles.Count == 0)
        {
            return;
        }

        var required = requiredAnyRoles
            .Select(r => r.Trim().ToLowerInvariant())
            .Where(r => r.Length > 0)
            .ToHashSet(StringComparer.Ordinal);

        var roles = await _db.PartyRoles.AsNoTracking()
            .Where(r => r.PartyId == partyId && r.IsActive)
            .Select(r => r.RoleCode)
            .ToListAsync(cancellationToken);

        if (!roles.Any(r => required.Contains(r)))
        {
            var labels = string.Join(" / ", required);
            throw new ConflictAppException(
                $"Đối tác {party.Code} chưa có vai trò phù hợp ({labels}) để {purposeVi}.");
        }
    }

    public async Task EnsureCreditAllowsArRecognizeAsync(
        Guid? partyId,
        decimal additionalAmount,
        string currencyCode,
        CancellationToken cancellationToken)
    {
        if (partyId is null)
        {
            return;
        }

        var eval = await EvaluateCreditAsync(partyId.Value, additionalAmount, currencyCode, cancellationToken);
        if (eval.WouldBlock)
        {
            throw new ConflictAppException(eval.Message ?? "Vượt hạn mức công nợ của đối tác.");
        }
    }

    public async Task<PartyCreditEvaluation> EvaluateCreditAsync(
        Guid partyId,
        decimal additionalAmount,
        string? currencyCode,
        CancellationToken cancellationToken)
    {
        var party = await _db.BusinessParties.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == partyId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy đối tác.");

        var mode = string.IsNullOrWhiteSpace(party.CreditControlMode)
            ? PartyCreditControlModes.Advisory
            : party.CreditControlMode.Trim().ToLowerInvariant();

        var limitCurrency = party.CreditLimitCurrencyCode?.Trim().ToUpperInvariant();
        var txCurrency = string.IsNullOrWhiteSpace(currencyCode)
            ? limitCurrency
            : currencyCode.Trim().ToUpperInvariant();

        decimal arSame = 0m;
        if (limitCurrency is not null)
        {
            var rows = await _db.AccountsReceivable.AsNoTracking()
                .Where(a =>
                    a.CounterpartyId == partyId
                    && a.RecordStatus == ApArRecordStatuses.Active
                    && a.CurrencyCode == limitCurrency)
                .Select(a => new { a.RecognizedAmount, a.AdjustmentAmount, a.FinalizedSettledAmount })
                .ToListAsync(cancellationToken);
            arSame = decimal.Round(
                rows.Sum(a => a.RecognizedAmount + a.AdjustmentAmount - a.FinalizedSettledAmount),
                4,
                MidpointRounding.AwayFromZero);
        }

        if (party.CreditLimit is null or <= 0)
        {
            return new PartyCreditEvaluation(
                mode,
                party.CreditLimit,
                limitCurrency,
                arSame,
                null,
                "none",
                false,
                null);
        }

        var sameCurrency = limitCurrency is not null
            && txCurrency is not null
            && string.Equals(limitCurrency, txCurrency, StringComparison.OrdinalIgnoreCase);

        if (!sameCurrency)
        {
            return new PartyCreditEvaluation(
                mode,
                party.CreditLimit,
                limitCurrency,
                arSame,
                Utilization(arSame, party.CreditLimit.Value),
                StatusOf(arSame, party.CreditLimit.Value),
                false,
                "Hạn mức và số ghi nhận khác tiền tệ — không chặn cứng (không quy đổi tỷ giá im lặng).");
        }

        var projected = decimal.Round(arSame + additionalAmount, 4, MidpointRounding.AwayFromZero);
        var over = projected > party.CreditLimit.Value;
        var wouldBlock = mode == PartyCreditControlModes.Block && over;
        var status = StatusOf(projected, party.CreditLimit.Value);
        string? message = null;
        if (over)
        {
            message =
                $"Công nợ phải thu dự kiến {projected:0.##} {limitCurrency} vượt hạn mức " +
                $"{party.CreditLimit.Value:0.##} {limitCurrency} của đối tác {party.Code}.";
        }

        return new PartyCreditEvaluation(
            mode,
            party.CreditLimit,
            limitCurrency,
            arSame,
            Utilization(projected, party.CreditLimit.Value),
            status,
            wouldBlock,
            message);
    }

    public async Task<(decimal ApOutstanding, decimal ArOutstanding, int OpenApCount, int OpenArCount)> GetOpenExposureTotalsAsync(
        Guid partyId,
        CancellationToken cancellationToken)
    {
        var apRows = await _db.AccountsPayable.AsNoTracking()
            .Where(a => a.CounterpartyId == partyId && a.RecordStatus == ApArRecordStatuses.Active)
            .Select(a => new { a.RecognizedAmount, a.AdjustmentAmount, a.FinalizedSettledAmount })
            .ToListAsync(cancellationToken);
        var arRows = await _db.AccountsReceivable.AsNoTracking()
            .Where(a => a.CounterpartyId == partyId && a.RecordStatus == ApArRecordStatuses.Active)
            .Select(a => new { a.RecognizedAmount, a.AdjustmentAmount, a.FinalizedSettledAmount })
            .ToListAsync(cancellationToken);

        var apOpen = apRows
            .Select(a => a.RecognizedAmount + a.AdjustmentAmount - a.FinalizedSettledAmount)
            .Where(v => v > 0)
            .ToList();
        var arOpen = arRows
            .Select(a => a.RecognizedAmount + a.AdjustmentAmount - a.FinalizedSettledAmount)
            .Where(v => v > 0)
            .ToList();

        return (
            decimal.Round(apOpen.Sum(), 4, MidpointRounding.AwayFromZero),
            decimal.Round(arOpen.Sum(), 4, MidpointRounding.AwayFromZero),
            apOpen.Count,
            arOpen.Count);
    }

    private static decimal Utilization(decimal outstanding, decimal limit) =>
        limit <= 0
            ? 0
            : decimal.Round(outstanding / limit * 100m, 1, MidpointRounding.AwayFromZero);

    private static string StatusOf(decimal outstanding, decimal limit)
    {
        if (outstanding > limit)
        {
            return "over";
        }

        if (outstanding >= limit * WatchRatio)
        {
            return "watch";
        }

        return "ok";
    }
}
