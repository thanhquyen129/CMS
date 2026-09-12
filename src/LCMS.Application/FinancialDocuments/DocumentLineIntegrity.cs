using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.FinancialDocuments.Commands;

/// <summary>ADR-0012 — sum vs header and match locks for document lines.</summary>
public static class DocumentLineIntegrity
{
    public const decimal MoneyEpsilon = 0.0001m;

    public static decimal RoundMoney(decimal amount) =>
        decimal.Round(amount, 4, MidpointRounding.AwayFromZero);

    public static async Task<decimal> SumLineAmountsAsync(
        IQueryable<FinancialDocumentLine> lines,
        Guid documentId,
        CancellationToken cancellationToken,
        Guid? excludeLineId = null)
    {
        var query = lines.AsNoTracking().Where(l => l.DocumentId == documentId);
        if (excludeLineId.HasValue)
        {
            query = query.Where(l => l.Id != excludeLineId.Value);
        }

        // SQLite cannot translate Sum(decimal) — materialize then sum.
        var amounts = await query.Select(l => l.Amount).ToListAsync(cancellationToken);
        return amounts.Sum();
    }

    public static void EnsureSumDoesNotExceedHeader(decimal linesSum, decimal headerTotal, string currencyCode)
    {
        if (linesSum > headerTotal + MoneyEpsilon)
        {
            throw new ConflictAppException(
                $"Tổng dòng ({linesSum:0.####} {currencyCode}) vượt tổng chứng từ ({headerTotal:0.####} {currencyCode}).");
        }
    }

    public static void EnsureSumEqualsHeaderForAccept(decimal linesSum, decimal headerTotal, string currencyCode, int lineCount)
    {
        if (headerTotal > MoneyEpsilon && lineCount == 0)
        {
            throw new ConflictAppException(
                "Chấp nhận yêu cầu ít nhất một dòng chứng từ khi tổng chứng từ > 0 (ADR-0012).");
        }

        if (Math.Abs(linesSum - headerTotal) > MoneyEpsilon)
        {
            throw new ConflictAppException(
                $"Chấp nhận yêu cầu tổng dòng = tổng chứng từ (ADR-0012). Hiện {linesSum:0.####} ≠ {headerTotal:0.####} {currencyCode}.");
        }
    }

    public static void EnsureDocumentAllowsLineDraft(FinancialDocument document)
    {
        if (!string.Equals(document.ReceiptStatus, FinancialDocumentReceiptStatuses.Received, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictAppException("Chỉ thao tác dòng trên chứng từ đã nhận.");
        }

        if (!string.Equals(document.RecordStatus, FinancialDocumentRecordStatuses.Active, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictAppException("Chứng từ không còn hiệu lực.");
        }
    }

    public static bool IsAccepted(FinancialDocument document) =>
        string.Equals(document.AcceptanceStatus, FinancialDocumentAcceptanceStatuses.Accepted, StringComparison.OrdinalIgnoreCase);

    public static void EnsureLineUnmatched(FinancialDocumentLine line)
    {
        if (line.MatchedAmount > MoneyEpsilon)
        {
            throw new ConflictAppException("Dòng đã khớp một phần/toàn phần — đảo chi tiết khớp trước khi sửa/xóa.");
        }
    }
}
