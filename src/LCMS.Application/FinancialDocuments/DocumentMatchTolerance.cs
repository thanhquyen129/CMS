using LCMS.Domain.Entities;

namespace LCMS.Application.FinancialDocuments;

/// <summary>Shared C-007 tolerance math for document matching.</summary>
public static class DocumentMatchTolerance
{
    public static decimal EffectiveTolerance(decimal lineAmount, decimal absolute, decimal percent)
    {
        var fromPercent = percent <= 0m
            ? 0m
            : decimal.Round(lineAmount * percent / 100m, 4, MidpointRounding.AwayFromZero);
        return Math.Max(absolute, fromPercent);
    }

    public static bool IsActiveDetail(string? detailStatus) =>
        string.IsNullOrWhiteSpace(detailStatus)
        || string.Equals(detailStatus, DocumentMatchDetailStatuses.Active, StringComparison.OrdinalIgnoreCase);
}
