using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>Table: permissions — global Action code catalog (not tenant-scoped).</summary>
public sealed class Permission : EntityBase
{
    /// <summary>Action code, e.g. bill.create, master.party.manage.</summary>
    public string ActionCode { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}
