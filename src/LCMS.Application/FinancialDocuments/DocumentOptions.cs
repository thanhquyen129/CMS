namespace LCMS.Application.FinancialDocuments;

/// <summary>
/// Document matching policy — tolerance (C-007) + duplicate control + accept-before-match (Pass 2 Sprint 6 FULL).
/// </summary>
public sealed class DocumentOptions
{
    public const string SectionName = "Documents";

    /// <summary>Absolute tolerance added to open amount when matching (default 0).</summary>
    public decimal DefaultToleranceAbsolute { get; set; }

    /// <summary>
    /// Percent of line face amount allowed as over-match (stub).
    /// Effective line tolerance = max(absolute, amount × percent / 100).
    /// </summary>
    public decimal DefaultTolerancePercent { get; set; }

    /// <summary>
    /// When true, reject receive if (tenant, document_type, document_no, counterparty) already exists (IDX-006 / BR-FIN-023).
    /// </summary>
    public bool EnforceDuplicateControl { get; set; } = true;

    /// <summary>When true, primary document (and source line document) must be Accepted before match.</summary>
    public bool RequireAcceptBeforeMatch { get; set; } = true;
}
