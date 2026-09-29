using LCMS.Application.Abstractions;
using LCMS.Application.Identity;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.OperationalReferences.Edit;

public sealed record OperationalFieldStateDto(
    string Code,
    string Label,
    string Kind,
    string Group,
    string? Value,
    string Source,
    string SourceLabel,
    bool Editable,
    bool RequiresReason,
    string? LockReason,
    bool RatingRelevant,
    string? SourceValue,
    string? OverrideReason);

public sealed record OperationalRatingStatusDto(
    Guid RatingId,
    Guid BillId,
    string BillNo,
    Guid RateVersionId,
    DateTimeOffset RatedAt,
    decimal? ChargeableWeightKg,
    string? ChargeableBasis,
    bool Stale,
    DateTimeOffset? StaleAt,
    string? StaleReason);

public sealed record OperationalOptionDto(string Value, string Label);

public sealed record OperationalReferenceEditDto(
    string ObjectType,
    Guid ObjectId,
    string Code,
    string ObjectLabel,
    string? SourceSystem,
    string SourceLabel,
    bool CanEdit,
    bool CanOverrideSource,
    bool CanOverrideChargeable,
    string? ReadOnlyReason,
    string RowVersion,
    IReadOnlyList<OperationalFieldStateDto> Fields,
    ChargeableWeightStateDto? Chargeable,
    IReadOnlyList<OperationalRatingStatusDto> CurrentRatings,
    int LinkedBillCount,
    IReadOnlyList<OperationalOptionDto> Commodities);

public sealed record GetOperationalReferenceEditQuery(string ObjectType, Guid ObjectId) : IRequest<OperationalReferenceEditDto>;

public sealed class GetOperationalReferenceEditQueryHandler
    : IRequestHandler<GetOperationalReferenceEditQuery, OperationalReferenceEditDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IPermissionService _permissions;
    private readonly ICurrentUserContext _user;
    private readonly IOrganizationHierarchyService _orgHierarchy;

    public GetOperationalReferenceEditQueryHandler(
        ILcmsDbContext db,
        ITenantContext tenant,
        IPermissionService permissions,
        ICurrentUserContext user,
        IOrganizationHierarchyService orgHierarchy)
    {
        _db = db;
        _tenant = tenant;
        _permissions = permissions;
        _user = user;
        _orgHierarchy = orgHierarchy;
    }

    public async Task<OperationalReferenceEditDto> Handle(GetOperationalReferenceEditQuery request, CancellationToken cancellationToken)
    {
        if (!_tenant.HasTenant)
        {
            throw new Common.Exceptions.TenantRequiredAppException();
        }

        var type = OperationalReferenceRecord.NormalizeType(request.ObjectType);
        var record = await OperationalReferenceRecord.LoadAsync(_db, type, request.ObjectId, track: false, cancellationToken);
        var access = new OperationalReferenceAccess(_db, _permissions, _user, _orgHierarchy);
        await access.EnsureAsync(record, PermissionCodes.BillRead, "Bạn không có quyền xem tham chiếu vận hành.", cancellationToken);

        var canEdit = await _permissions.HasPermissionAsync(OperationalReferenceAccess.EditPermission(type), cancellationToken);
        var canOverrideSource = await _permissions.HasPermissionAsync(PermissionCodes.OperationalSourceOverride, cancellationToken);
        var canOverrideCw = await _permissions.HasPermissionAsync(PermissionCodes.RateQuantityOverride, cancellationToken);

        var fields = OperationalFieldCatalog.For(type)
            .Select(def => OperationalFieldStateResolver.Resolve(record, def, canEdit, canOverrideSource))
            .ToList();

        ChargeableWeightStateDto? chargeable = null;
        if (OperationalObjectTypes.CargoParents.Contains(type))
        {
            record.Overrides.TryGetValue(OperationalFieldCodes.ChargeableWeightKg, out var cwOverride);
            chargeable = ChargeableWeightPolicy.Resolve(record.Measures, cwOverride, record.TransportMode, record.SourceSystem);
        }

        var billIds = await OperationalReferenceAccess.LinkedBillIdsAsync(_db, type, record.Id, cancellationToken);
        var ratings = billIds.Count == 0
            ? []
            : await (
                from r in _db.Ratings.AsNoTracking()
                join b in _db.Bills.AsNoTracking() on r.BillId equals b.Id
                where billIds.Contains(r.BillId) && r.Status == RatingStatuses.Completed
                select new OperationalRatingStatusDto(
                    r.Id,
                    r.BillId,
                    b.BillNo,
                    r.RateVersionId,
                    r.RatedAt,
                    r.ChargeableWeightKg,
                    r.ChargeableBasis,
                    r.StaleAt != null,
                    r.StaleAt,
                    r.StaleReason))
                .ToListAsync(cancellationToken);

        var commodities = OperationalObjectTypes.CargoParents.Contains(type)
            ? (await _db.CommodityTypes.AsNoTracking()
                    .Where(c => c.IsActive)
                    .Select(c => new { c.Id, c.Code, c.Name })
                    .ToListAsync(cancellationToken))
                .OrderBy(c => c.Name, StringComparer.CurrentCulture)
                .Select(c => new OperationalOptionDto(c.Id.ToString(), $"{c.Code} — {c.Name}"))
                .ToList()
            : [];

        return new OperationalReferenceEditDto(
            type,
            record.Id,
            record.Code,
            OperationalFieldCatalog.ObjectLabel(type),
            record.SourceSystem,
            OperationalFieldStateResolver.ObjectSourceLabel(record),
            canEdit,
            canOverrideSource,
            canOverrideCw,
            canEdit ? null : "Bạn không có quyền sửa thông tin này.",
            Convert.ToBase64String(record.RowVersion),
            fields,
            chargeable,
            ratings.OrderByDescending(r => r.RatedAt).ToList(),
            type == OperationalObjectTypes.Bill ? 0 : billIds.Count,
            commodities);
    }
}

/// <summary>Source-of-Truth policy per field (ADR-0039 D04/D08).</summary>
internal static class OperationalFieldStateResolver
{
    public const string SourceManual = "manual";
    public const string SourceImport = "import";
    public const string SourceApi = "api";
    public const string SourceSystem = "system";
    public const string SourceOverride = "override";

    public static string ObjectSourceLabel(OperationalReferenceRecord record) =>
        record.IsExternal ? $"Hệ thống nguồn: {record.SourceSystem}" : "Nhập thủ công trên CMS";

    public static OperationalFieldStateDto Resolve(
        OperationalReferenceRecord record,
        OperationalFieldDef def,
        bool canEdit,
        bool canOverrideSource)
    {
        var value = record.Get(def.Code);
        var (source, label, owner) = SourceOf(record, def, value);
        record.Overrides.TryGetValue(def.Code, out var overrideRow);

        var requiresReason = source is SourceOverride or SourceApi || (source == SourceImport && value is not null);
        var editable = canEdit;
        string? lockReason = canEdit ? null : "Bạn không có quyền sửa.";
        if (canEdit && source == SourceApi && !canOverrideSource)
        {
            editable = false;
            lockReason = $"Trường thuộc hệ thống {owner}. Cần quyền Ghi đè dữ liệu hệ thống nguồn.";
        }

        return new OperationalFieldStateDto(
            def.Code,
            def.Label,
            def.Kind,
            def.Group,
            value,
            source,
            label,
            editable,
            requiresReason,
            lockReason,
            def.RatingRelevant,
            overrideRow?.SourceValue,
            overrideRow?.Reason);
    }

    public static (string Source, string Label, string? Owner) SourceOf(OperationalReferenceRecord record, OperationalFieldDef def, string? value)
    {
        if (record.Overrides.ContainsKey(def.Code))
        {
            return (SourceOverride, "Ghi đè", null);
        }

        if (def.MeasureCode is not null)
        {
            var measure = record.Measure(def.MeasureCode);
            return measure?.SourceChannel switch
            {
                MeasureSourceChannels.Import => (SourceImport, ChargeableWeightPolicy.SourceLabel(MeasureSourceChannels.Import, record.SourceSystem), record.SourceSystem),
                MeasureSourceChannels.Api => (SourceApi, ChargeableWeightPolicy.SourceLabel(MeasureSourceChannels.Api, record.SourceSystem), record.SourceSystem),
                MeasureSourceChannels.System => (SourceSystem, "Hệ thống tính", null),
                MeasureSourceChannels.Override => (SourceOverride, "Ghi đè", null),
                _ => record.IsExternal && measure is null && value is not null
                    ? (SourceImport, ChargeableWeightPolicy.SourceLabel(MeasureSourceChannels.Import, record.SourceSystem), record.SourceSystem)
                    : (SourceManual, "Nhập thủ công", null)
            };
        }

        var owner = record.ExternalOwner(def);
        if (owner is not null)
        {
            return (SourceApi, $"API ({owner})", owner);
        }

        if (record.IsExternal && value is not null)
        {
            return (SourceImport, $"Nguồn ({record.SourceSystem})", record.SourceSystem);
        }

        return (SourceManual, "Nhập thủ công", null);
    }
}
