using System.Text.Json;
using System.Text.Json.Serialization;

namespace LCMS.Application.Audit;

/// <summary>
/// Compact JSON snapshots for audit_events before/after (Pass 2 Sprint 12 FULL).
/// </summary>
public static class AuditJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string Serialize(object value) =>
        JsonSerializer.Serialize(value, Options);
}
