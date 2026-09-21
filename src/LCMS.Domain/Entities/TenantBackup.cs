using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>
/// Table: tenant_backups — logical master/config snapshot (ADR-0021). Not a Postgres PITR substitute.
/// </summary>
public sealed class TenantBackup : TenantEntityBase
{
    public string Kind { get; set; } = TenantBackupKinds.LogicalMaster;
    public string Status { get; set; } = TenantBackupStatuses.Completed;
    public string PayloadJson { get; set; } = "{}";
    public string ChecksumSha256 { get; set; } = string.Empty;
    public int ByteSize { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset? RestoredAt { get; set; }
    public Guid? RestoredBy { get; set; }
}

public static class TenantBackupKinds
{
    public const string LogicalMaster = "logical_master";
}

public static class TenantBackupStatuses
{
    public const string Completed = "completed";
    public const string Failed = "failed";
}
