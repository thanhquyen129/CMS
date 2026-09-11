using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>Table: role_permissions — Role × Action × Data Scope (independent dimensions).</summary>
public sealed class RolePermission : TenantEntityBase
{
    public Guid RoleId { get; set; }
    public Guid PermissionId { get; set; }

    /// <summary>Data Scope: all | organization | own — independent of Action permission.</summary>
    public string DataScope { get; set; } = DataScopes.All;
}

public static class DataScopes
{
    public const string All = "all";
    public const string Organization = "organization";
    public const string Own = "own";

    public static readonly IReadOnlyList<string> AllValues = [All, Organization, Own];

    public static bool IsValid(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && AllValues.Contains(value.Trim().ToLowerInvariant());

    /// <summary>Widen rank: all (3) &gt; organization (2) &gt; own (1).</summary>
    public static int Rank(string scope) => scope.Trim().ToLowerInvariant() switch
    {
        All => 3,
        Organization => 2,
        Own => 1,
        _ => 0
    };

    public static string Widen(IEnumerable<string> scopes)
    {
        var best = Own;
        var bestRank = 0;
        foreach (var raw in scopes)
        {
            var s = raw.Trim().ToLowerInvariant();
            var r = Rank(s);
            if (r > bestRank)
            {
                bestRank = r;
                best = s;
            }
        }

        return bestRank == 0 ? Own : best;
    }
}
