namespace LCMS.Application.Approvals;

/// <summary>
/// Approver matrix: object type + min amount → required approval level (P12).
/// Config-first; tenant DB override deferred to P19/P20.
/// </summary>
public sealed class ApprovalMatrixOptions
{
    public const string SectionName = "ApprovalMatrix";

    /// <summary>Rules evaluated highest MinAmount first within matching ObjectType.</summary>
    public List<ApprovalMatrixRule> Rules { get; set; } =
    [
        new() { ObjectType = "accounts_payable", MinAmount = 0, RequiredLevel = 1 },
        new() { ObjectType = "accounts_payable", MinAmount = 10000, RequiredLevel = 2 },
        new() { ObjectType = "accounts_receivable", MinAmount = 0, RequiredLevel = 1 },
        new() { ObjectType = "accounts_receivable", MinAmount = 10000, RequiredLevel = 2 }
    ];
}

public sealed class ApprovalMatrixRule
{
    /// <summary>ApprovalObjectTypes value, e.g. accounts_payable.</summary>
    public string ObjectType { get; set; } = string.Empty;

    /// <summary>Inclusive minimum amount (in object currency / base) for this level.</summary>
    public decimal MinAmount { get; set; }

    /// <summary>1 or 2 (multi-step).</summary>
    public int RequiredLevel { get; set; } = 1;
}

public static class ApprovalMatrixResolver
{
    public static int ResolveRequiredLevel(
        ApprovalMatrixOptions options,
        string objectType,
        decimal amount)
    {
        var type = objectType.Trim().ToLowerInvariant();
        var rules = (options.Rules ?? [])
            .Where(r => string.Equals(r.ObjectType?.Trim(), type, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => r.MinAmount)
            .ToList();

        foreach (var rule in rules)
        {
            if (amount >= rule.MinAmount)
            {
                return Math.Clamp(rule.RequiredLevel, 1, 2);
            }
        }

        return 1;
    }
}
