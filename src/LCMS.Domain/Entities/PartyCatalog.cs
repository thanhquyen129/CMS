namespace LCMS.Domain.Entities;

public static class PartyKinds
{
    public const string Organization = "organization";
    public const string Individual = "individual";

    public static readonly IReadOnlyList<string> Catalog = [Organization, Individual];

    public static bool IsKnown(string? code) =>
        !string.IsNullOrWhiteSpace(code)
        && Catalog.Contains(code.Trim().ToLowerInvariant());
}

public static class PartyLegalTypes
{
    public const string Company = "company";
    public const string Llc = "llc";
    public const string Jsc = "jsc";
    public const string Individual = "individual";
    public const string Household = "household";
    public const string Foreign = "foreign";
    public const string Other = "other";

    public static readonly IReadOnlyList<string> Catalog =
    [
        Company, Llc, Jsc, Individual, Household, Foreign, Other
    ];

    public static bool IsKnown(string? code) =>
        string.IsNullOrWhiteSpace(code)
        || Catalog.Contains(code.Trim().ToLowerInvariant());
}

public static class PartyCreditControlModes
{
    public const string Advisory = "advisory";
    public const string Warn = "warn";
    public const string Block = "block";

    public static readonly IReadOnlyList<string> Catalog = [Advisory, Warn, Block];

    public static bool IsKnown(string? code) =>
        !string.IsNullOrWhiteSpace(code)
        && Catalog.Contains(code.Trim().ToLowerInvariant());
}

public static class PartyContactFunctions
{
    public const string General = "general";
    public const string Billing = "billing";
    public const string Ops = "ops";
    public const string Legal = "legal";

    public static readonly IReadOnlyList<string> Catalog = [General, Billing, Ops, Legal];

    public static bool IsKnown(string? code) =>
        !string.IsNullOrWhiteSpace(code)
        && Catalog.Contains(code.Trim().ToLowerInvariant());
}

public static class PartyStatusCodes
{
    public const string Active = "active";
    public const string Inactive = "inactive";
    public const string Blocked = "blocked";

    public static string FromFlags(bool isActive, bool isBlocked) =>
        isBlocked ? Blocked : isActive ? Active : Inactive;
}
