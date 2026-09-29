using LCMS.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.OperationalReferences.Edit;

/// <summary>
/// Sync / import writes must not silently erase a controlled override (ADR-0039 D08):
/// while an override is active the incoming source value is only recorded as SourceValue.
/// </summary>
internal static class OperationalOverrideGuard
{
    public static async Task<bool> CaptureSourceAsync(
        ILcmsDbContext db,
        string objectType,
        Guid objectId,
        string fieldCode,
        string? incomingValue,
        CancellationToken cancellationToken)
    {
        var row = await db.OperationalFieldOverrides.FirstOrDefaultAsync(
            o => o.ObjectType == objectType && o.ObjectId == objectId && o.FieldCode == fieldCode,
            cancellationToken);
        if (row is null)
        {
            return false;
        }

        if (incomingValue is not null)
        {
            row.SourceValue = incomingValue;
        }

        return true;
    }

    /// <summary>
    /// After a sync upsert has written entity fields: keep the incoming values as SourceValue
    /// and restore the overridden values. Measures are guarded separately at write time.
    /// </summary>
    public static async Task ReapplyEntityOverridesAsync(
        ILcmsDbContext db,
        string objectType,
        Guid objectId,
        CancellationToken cancellationToken)
    {
        var active = await db.OperationalFieldOverrides.AnyAsync(
            o => o.ObjectType == objectType && o.ObjectId == objectId,
            cancellationToken);
        if (!active)
        {
            return;
        }

        var record = await OperationalReferenceRecord.LoadAsync(db, objectType, objectId, track: true, cancellationToken);
        foreach (var row in record.Overrides.Values)
        {
            var def = OperationalFieldCatalog.Find(objectType, row.FieldCode);
            if (def is null || def.MeasureCode is not null)
            {
                continue;
            }

            var incoming = record.Get(row.FieldCode);
            if (string.Equals(incoming, row.OverrideValue, StringComparison.Ordinal))
            {
                continue;
            }

            if (incoming is not null)
            {
                row.SourceValue = incoming;
            }

            record.SetEntityField(row.FieldCode, row.OverrideValue);
        }

        record.FlushContext();
    }

    public static Task<bool> CaptureSourceAsync(
        ILcmsDbContext db,
        string objectType,
        Guid objectId,
        string fieldCode,
        decimal? incomingValue,
        CancellationToken cancellationToken) =>
        CaptureSourceAsync(db, objectType, objectId, fieldCode, OperationalFieldValues.FormatDecimal(incomingValue), cancellationToken);
}
