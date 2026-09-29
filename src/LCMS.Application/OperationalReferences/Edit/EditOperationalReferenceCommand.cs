using System.Globalization;
using System.Text.Json;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.Identity;
using LCMS.Application.Ratings;
using LCMS.Application.ReferenceMasters;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.OperationalReferences.Edit;

/// <summary>
/// "Sửa thông tin" for Bill / Order / Shipment / Chặng / Chuyến (ADR-0039).
/// Values are canonical strings (invariant decimals, ISO-8601 UTC). Never touches Cost / Revenue / AP / AR.
/// </summary>
public sealed record EditOperationalReferenceCommand(
    string ObjectType,
    Guid ObjectId,
    IReadOnlyDictionary<string, string?> Changes,
    string? Reason,
    IReadOnlyList<string>? RevertFields = null,
    string? IfMatch = null) : IRequest<OperationalReferenceEditResult>;

public sealed record OperationalReferenceEditResult(
    IReadOnlyList<string> ChangedFields,
    int RatingsMarkedStale,
    string RowVersion);

public sealed class EditOperationalReferenceCommandHandler
    : IRequestHandler<EditOperationalReferenceCommand, OperationalReferenceEditResult>
{
    private static readonly HashSet<string> CwInputs = new(StringComparer.Ordinal)
    {
        OperationalFieldCodes.GrossWeightKg, OperationalFieldCodes.VolumeCbm, OperationalFieldCodes.TransportMode
    };

    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IPermissionService _permissions;
    private readonly ICurrentUserContext _user;
    private readonly IOrganizationHierarchyService _orgHierarchy;
    private readonly IAuditWriter _audit;
    private readonly ICanonicalPlaceBinder _places;
    private readonly IRatingStalenessService _staleness;

    public EditOperationalReferenceCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenant,
        IPermissionService permissions,
        ICurrentUserContext user,
        IOrganizationHierarchyService orgHierarchy,
        IAuditWriter audit,
        ICanonicalPlaceBinder places,
        IRatingStalenessService staleness)
    {
        _db = db;
        _tenant = tenant;
        _permissions = permissions;
        _user = user;
        _orgHierarchy = orgHierarchy;
        _audit = audit;
        _places = places;
        _staleness = staleness;
    }

    public async Task<OperationalReferenceEditResult> Handle(EditOperationalReferenceCommand request, CancellationToken cancellationToken)
    {
        if (!_tenant.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var type = OperationalReferenceRecord.NormalizeType(request.ObjectType);
        var record = await OperationalReferenceRecord.LoadAsync(_db, type, request.ObjectId, track: true, cancellationToken);
        var access = new OperationalReferenceAccess(_db, _permissions, _user, _orgHierarchy);
        await access.EnsureAsync(
            record,
            OperationalReferenceAccess.EditPermission(type),
            $"Bạn không có quyền sửa thông tin {OperationalFieldCatalog.ObjectLabel(type)}.",
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.IfMatch)
            && !string.Equals(request.IfMatch.Trim().Trim('"'), Convert.ToBase64String(record.RowVersion), StringComparison.Ordinal))
        {
            throw new ConflictAppException();
        }

        var reason = OperationalFieldValues.Trim(request.Reason);
        if (reason is { Length: > 500 })
        {
            throw new ValidationAppException(new Dictionary<string, string[]> { ["reason"] = ["Lý do tối đa 500 ký tự."] });
        }

        var canOverrideSource = await _permissions.HasPermissionAsync(PermissionCodes.OperationalSourceOverride, cancellationToken);
        var isCargoParent = OperationalObjectTypes.CargoParents.Contains(type);
        record.Overrides.TryGetValue(OperationalFieldCodes.ChargeableWeightKg, out var cwOverrideBefore);
        var cwBefore = isCargoParent
            ? ChargeableWeightPolicy.Resolve(record.Measures, cwOverrideBefore, record.TransportMode, record.SourceSystem)
            : null;

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var normalized = new List<(OperationalFieldDef Def, string? Value)>();
        foreach (var (rawCode, rawValue) in request.Changes ?? new Dictionary<string, string?>())
        {
            var code = (rawCode ?? string.Empty).Trim().ToLowerInvariant();
            var def = OperationalFieldCatalog.Find(type, code);
            if (def is null)
            {
                errors[code] = [$"Trường {code} không sửa được trên {OperationalFieldCatalog.ObjectLabel(type)}."];
                continue;
            }

            var (ok, value, message) = await NormalizeAsync(def, rawValue, cancellationToken);
            if (!ok)
            {
                errors[code] = [message!];
                continue;
            }

            normalized.Add((def, value));
        }

        if (errors.Count > 0)
        {
            throw new ValidationAppException(errors);
        }

        var changes = new List<FieldChange>();
        foreach (var (def, value) in normalized.Where(n => n.Def.Code != OperationalFieldCodes.ChargeableWeightKg))
        {
            var change = await ApplyFieldAsync(record, def, value, reason, canOverrideSource, cancellationToken);
            if (change is not null)
            {
                changes.Add(change);
            }
        }

        foreach (var code in (request.RevertFields ?? []).Select(c => c.Trim().ToLowerInvariant()).Distinct())
        {
            var change = await RevertAsync(record, code, reason, cancellationToken);
            if (change is not null)
            {
                changes.Add(change);
            }
        }

        var cwRequested = normalized.FirstOrDefault(n => n.Def.Code == OperationalFieldCodes.ChargeableWeightKg);
        if (cwRequested.Def is not null && cwBefore is not null)
        {
            var change = await ApplyChargeableAsync(record, cwBefore, cwRequested.Value, reason, cancellationToken);
            if (change is not null)
            {
                changes.Add(change);
            }
        }

        if (isCargoParent && changes.Any(c => CwInputs.Contains(c.Code)))
        {
            RecalculateSystemChargeable(record);
        }

        ValidateSchedule(record);

        var changedCodes = changes.Select(c => c.Code).Distinct().ToList();
        if (isCargoParent && cwBefore is not null)
        {
            record.Overrides.TryGetValue(OperationalFieldCodes.ChargeableWeightKg, out var cwOverrideAfter);
            var cwAfter = ChargeableWeightPolicy.Resolve(record.Measures, cwOverrideAfter, record.TransportMode, record.SourceSystem);
            if (cwAfter.Value != cwBefore.Value && !changedCodes.Contains(OperationalFieldCodes.ChargeableWeightKg))
            {
                changedCodes.Add(OperationalFieldCodes.ChargeableWeightKg);
                changes.Add(new FieldChange(
                    OperationalFieldCodes.ChargeableWeightKg,
                    OperationalFieldValues.FormatDecimal(cwBefore.Value),
                    OperationalFieldValues.FormatDecimal(cwAfter.Value)));
            }
        }

        if (changedCodes.Count == 0)
        {
            return new OperationalReferenceEditResult([], 0, Convert.ToBase64String(record.RowVersion));
        }

        record.FlushContext();

        var staleReason = StaleReason(type, record.Code, changes);
        var marked = type == OperationalObjectTypes.Bill
            ? await _staleness.MarkForBillChangeAsync(record.Id, changedCodes, staleReason, cancellationToken)
            : await _staleness.MarkLinkedBillsAsync(type, record.Id, changedCodes, staleReason, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
        return new OperationalReferenceEditResult(changedCodes, marked, Convert.ToBase64String(RowVersionOf(record)));
    }

    private sealed record FieldChange(string Code, string? OldValue, string? NewValue);

    private async Task<FieldChange?> ApplyFieldAsync(
        OperationalReferenceRecord record,
        OperationalFieldDef def,
        string? value,
        string? reason,
        bool canOverrideSource,
        CancellationToken cancellationToken)
    {
        var old = record.Get(def.Code);
        if (def.Code is OperationalFieldCodes.OriginCode or OperationalFieldCodes.DestinationCode && value is not null)
        {
            var bound = await _places.BindAsync(value, def.Label, cancellationToken);
            value = bound.Code ?? value;
            BindLocation(record, def.Code, bound.LocationId);
        }

        if (string.Equals(old, value, StringComparison.Ordinal))
        {
            return null;
        }

        var (source, sourceLabel, owner) = OperationalFieldStateResolver.SourceOf(record, def, old);
        var isOverride = source is OperationalFieldStateResolver.SourceOverride or OperationalFieldStateResolver.SourceApi
            || (source == OperationalFieldStateResolver.SourceImport && old is not null);
        if (source == OperationalFieldStateResolver.SourceApi && !canOverrideSource)
        {
            throw new ForbiddenAppException(
                $"{def.Label} thuộc hệ thống {owner}. Bạn không có quyền ghi đè dữ liệu hệ thống nguồn.");
        }

        if (isOverride && reason is null)
        {
            throw new ConflictAppException(
                $"{def.Label} đang lấy từ {sourceLabel}. Nhập Lý do ghi đè để lưu giá trị mới.");
        }

        if (def.MeasureCode is not null)
        {
            if (value is null && isOverride)
            {
                throw new ConflictAppException(
                    $"Không xóa được {def.Label} lấy từ {sourceLabel}. Nhập giá trị mới hoặc Bỏ ghi đè.");
            }

            WriteMeasure(record, def, value is null ? null : decimal.Parse(value, CultureInfo.InvariantCulture),
                isOverride ? MeasureSourceChannels.Override : MeasureSourceChannels.Manual);
        }
        else
        {
            record.SetEntityField(def.Code, value);
        }

        if (isOverride)
        {
            UpsertOverride(record, def.Code, old, value, SourceChannelFor(record, def, source, owner), reason!);
            _audit.Append(
                AuditActions.OperationalFieldOverride,
                record.Type,
                record.Id,
                AuditValue(def, old, sourceLabel),
                AuditValue(def, value, "Ghi đè"),
                reason);
        }
        else
        {
            _audit.Append(
                AuditActions.OperationalFieldUpdate,
                record.Type,
                record.Id,
                AuditValue(def, old, sourceLabel),
                AuditValue(def, value, "Nhập thủ công"),
                reason);
        }

        return new FieldChange(def.Code, old, value);
    }

    private Task<FieldChange?> RevertAsync(
        OperationalReferenceRecord record,
        string code,
        string? reason,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        if (!record.Overrides.TryGetValue(code, out var row))
        {
            throw new ConflictAppException($"{OperationalFieldCatalog.Label(record.Type, code)} không có ghi đè để bỏ.");
        }

        var def = OperationalFieldCatalog.Find(record.Type, code)
            ?? throw new ConflictAppException($"Trường {code} không sửa được.");
        var old = code == OperationalFieldCodes.ChargeableWeightKg
            ? OperationalFieldValues.FormatDecimal(record.Measure(MeasureCodes.ChargeableWeightKg)?.Quantity)
            : record.Get(code);
        var restored = row.SourceValue;
        var channel = row.SourceChannel switch
        {
            MeasureSourceChannels.Import or MeasureSourceChannels.Api or MeasureSourceChannels.System or MeasureSourceChannels.Manual => row.SourceChannel,
            _ => MeasureSourceChannels.Import
        };

        if (def.MeasureCode is not null)
        {
            if (restored is null)
            {
                RemoveMeasure(record, def.MeasureCode);
                record.MirrorMeasureToContext(def.Code, null);
            }
            else
            {
                WriteMeasure(record, def, decimal.Parse(restored, CultureInfo.InvariantCulture), channel);
            }
        }
        else
        {
            record.SetEntityField(code, restored);
        }

        row.SoftDelete(_user.UserId);
        record.Overrides.Remove(code);
        _audit.Append(
            code == OperationalFieldCodes.ChargeableWeightKg ? AuditActions.MeasurementOverrideClear : AuditActions.OperationalFieldUpdate,
            record.Type,
            record.Id,
            AuditValue(def, old, "Ghi đè"),
            AuditValue(def, restored, ChargeableWeightPolicy.SourceLabel(channel, record.SourceSystem)),
            reason ?? "Bỏ ghi đè — dùng lại giá trị nguồn");
        return Task.FromResult<FieldChange?>(new FieldChange(code, old, restored));
    }

    private async Task<FieldChange?> ApplyChargeableAsync(
        OperationalReferenceRecord record,
        ChargeableWeightStateDto before,
        string? value,
        string? reason,
        CancellationToken cancellationToken)
    {
        var def = OperationalFieldCatalog.ChargeableWeight;
        var old = OperationalFieldValues.FormatDecimal(before.Value);
        if (string.Equals(old, value, StringComparison.Ordinal))
        {
            return null;
        }

        var measure = record.Measure(MeasureCodes.ChargeableWeightKg);
        if (value is null)
        {
            if (before.State != ChargeableWeightStates.Manual || before.IsConfirmed)
            {
                throw new ConflictAppException(
                    "Không xóa được Trọng lượng tính cước đang lấy từ hệ thống/nguồn hoặc đã xác nhận. Dùng Bỏ ghi đè hoặc nhập giá trị mới.");
            }

            RemoveMeasure(record, MeasureCodes.ChargeableWeightKg);
            record.MirrorMeasureToContext(def.Code, null);
            _audit.Append(AuditActions.OperationalFieldUpdate, record.Type, record.Id,
                AuditValue(def, old, before.SourceLabel), AuditValue(def, null, "Chưa xác định"), reason);
            return new FieldChange(def.Code, old, null);
        }

        var amount = decimal.Parse(value, CultureInfo.InvariantCulture);
        var plainManual = before.State == ChargeableWeightStates.Missing
            || (before.State == ChargeableWeightStates.Manual && !before.IsConfirmed);
        if (plainManual)
        {
            WriteMeasure(record, def, amount, MeasureSourceChannels.Manual);
            _audit.Append(AuditActions.OperationalFieldUpdate, record.Type, record.Id,
                AuditValue(def, old, before.SourceLabel), AuditValue(def, value, "Nhập thủ công"), reason);
            return new FieldChange(def.Code, old, value);
        }

        if (reason is null)
        {
            throw new ConflictAppException(
                $"Trọng lượng tính cước đang là {before.SourceLabel}{(before.IsConfirmed ? " (đã xác nhận)" : string.Empty)}. Nhập Lý do ghi đè.");
        }

        await _permissions.EnsureAsync(
            PermissionCodes.RateQuantityOverride,
            "Bạn không có quyền ghi đè Trọng lượng tính cước.",
            cancellationToken);

        var sourceChannel = before.State == ChargeableWeightStates.Override
            ? record.Overrides.TryGetValue(def.Code, out var existing) ? existing.SourceChannel : MeasureSourceChannels.Manual
            : before.SourceChannel ?? MeasureSourceChannels.System;
        var sourceValue = before.State == ChargeableWeightStates.Override
            ? OperationalFieldValues.FormatDecimal(before.SourceValue)
            : old;
        WriteMeasure(record, def, amount, MeasureSourceChannels.Override);
        UpsertOverride(record, def.Code, sourceValue, value, sourceChannel, reason);
        if (measure is null)
        {
            record.Measure(MeasureCodes.ChargeableWeightKg)!.RuleCode = before.RuleCode;
        }

        _audit.Append(
            AuditActions.MeasurementOverride,
            record.Type,
            record.Id,
            AuditValue(def, old, before.SourceLabel),
            AuditValue(def, value, "Ghi đè"),
            reason);
        return new FieldChange(def.Code, old, value);
    }

    /// <summary>System CW follows its inputs. A changed value is never auto-confirmed (ADR-0039 Stale state).</summary>
    private void RecalculateSystemChargeable(OperationalReferenceRecord record)
    {
        var gross = record.Measure(MeasureCodes.GrossWeightKg)?.Quantity;
        var volume = record.Measure(MeasureCodes.VolumeCbm)?.Quantity;
        var computed = ChargeableWeightPolicy.Compute(record.TransportMode, gross, volume);
        var cw = record.Measure(MeasureCodes.ChargeableWeightKg);

        if (record.Overrides.TryGetValue(OperationalFieldCodes.ChargeableWeightKg, out var row)
            && row.SourceChannel == MeasureSourceChannels.System)
        {
            row.SourceValue = OperationalFieldValues.FormatDecimal(computed.Value);
            return;
        }

        if (cw is null || cw.SourceChannel != MeasureSourceChannels.System)
        {
            return;
        }

        var old = cw.Quantity;
        if (computed.Value is null)
        {
            RemoveMeasure(record, MeasureCodes.ChargeableWeightKg);
            record.MirrorMeasureToContext(OperationalFieldCodes.ChargeableWeightKg, null);
        }
        else if (computed.Value.Value != old)
        {
            cw.Quantity = computed.Value.Value;
            cw.RuleCode = computed.RuleCode;
            cw.IsConfirmed = false;
            cw.ConfirmedAt = null;
            record.MirrorMeasureToContext(OperationalFieldCodes.ChargeableWeightKg, computed.Value);
        }
        else
        {
            return;
        }

        _audit.Append(
            AuditActions.OperationalFieldUpdate,
            record.Type,
            record.Id,
            AuditValue(OperationalFieldCatalog.ChargeableWeight, OperationalFieldValues.FormatDecimal(old), "Hệ thống tính"),
            AuditValue(OperationalFieldCatalog.ChargeableWeight, OperationalFieldValues.FormatDecimal(computed.Value), "Hệ thống tính lại"),
            computed.MissingReason ?? "Đầu vào thay đổi — hệ thống tính lại, cần xác nhận lại.");
    }

    private void WriteMeasure(OperationalReferenceRecord record, OperationalFieldDef def, decimal? value, string channel)
    {
        if (value is null)
        {
            RemoveMeasure(record, def.MeasureCode!);
            record.MirrorMeasureToContext(def.Code, null);
            return;
        }

        var row = record.Measure(def.MeasureCode!);
        if (row is null)
        {
            row = new OperationalMeasurement
            {
                TenantId = _tenant.TenantId!.Value,
                ObjectType = record.Type,
                ObjectId = record.Id,
                MeasureCode = def.MeasureCode!,
                Uom = UomFor(def.MeasureCode!, record.TransportMode)
            };
            _db.OperationalMeasurements.Add(row);
            record.Measures.Add(row);
        }

        row.Quantity = value.Value;
        row.SourceChannel = channel;
        if (def.MeasureCode == MeasureCodes.ChargeableWeightKg)
        {
            row.IsConfirmed = false;
            row.ConfirmedAt = null;
        }

        record.MirrorMeasureToContext(def.Code, value);
    }

    private void RemoveMeasure(OperationalReferenceRecord record, string measureCode)
    {
        var row = record.Measure(measureCode);
        if (row is null)
        {
            return;
        }

        row.SoftDelete(_user.UserId);
        record.Measures.Remove(row);
    }

    private void UpsertOverride(
        OperationalReferenceRecord record,
        string code,
        string? sourceValue,
        string? overrideValue,
        string sourceChannel,
        string reason)
    {
        if (record.Overrides.TryGetValue(code, out var row))
        {
            row.OverrideValue = overrideValue;
            row.Reason = reason;
            row.OverriddenBy = _user.UserId;
            row.OverriddenAt = DateTimeOffset.UtcNow;
            return;
        }

        row = new OperationalFieldOverride
        {
            TenantId = _tenant.TenantId!.Value,
            ObjectType = record.Type,
            ObjectId = record.Id,
            FieldCode = code,
            SourceValue = sourceValue,
            OverrideValue = overrideValue,
            SourceChannel = sourceChannel,
            Reason = reason,
            OverriddenBy = _user.UserId,
            OverriddenAt = DateTimeOffset.UtcNow
        };
        _db.OperationalFieldOverrides.Add(row);
        record.Overrides[code] = row;
    }

    private static string SourceChannelFor(OperationalReferenceRecord record, OperationalFieldDef def, string source, string? owner)
    {
        if (def.MeasureCode is not null)
        {
            var channel = record.Measure(def.MeasureCode)?.SourceChannel;
            if (channel is MeasureSourceChannels.Import or MeasureSourceChannels.Api or MeasureSourceChannels.System)
            {
                return channel;
            }
        }

        return source switch
        {
            OperationalFieldStateResolver.SourceApi => owner ?? MeasureSourceChannels.Api,
            OperationalFieldStateResolver.SourceImport => MeasureSourceChannels.Import,
            _ => MeasureSourceChannels.Manual
        };
    }

    private static void BindLocation(OperationalReferenceRecord record, string code, Guid? locationId)
    {
        var origin = code == OperationalFieldCodes.OriginCode;
        if (record.Bill is not null)
        {
            if (origin) record.Bill.OriginLocationId = locationId; else record.Bill.DestinationLocationId = locationId;
        }

        if (record.Order is not null)
        {
            if (origin) record.Order.OriginLocationId = locationId; else record.Order.DestinationLocationId = locationId;
        }

        if (record.Shipment is not null)
        {
            if (origin) record.Shipment.OriginLocationId = locationId; else record.Shipment.DestinationLocationId = locationId;
        }

        if (record.Leg is not null)
        {
            if (origin) record.Leg.OriginLocationId = locationId; else record.Leg.DestinationLocationId = locationId;
        }
    }

    private static void ValidateSchedule(OperationalReferenceRecord record)
    {
        var etd = record.Bill?.EtdAt ?? record.Order?.EtdAt ?? record.Shipment?.EtdAt;
        var eta = record.Bill?.EtaAt ?? record.Order?.EtaAt ?? record.Shipment?.EtaAt;
        if (etd is not null && eta is not null && etd > eta)
        {
            throw new ValidationAppException(new Dictionary<string, string[]>
            {
                [OperationalFieldCodes.EtaAt] = ["ETD không được sau ETA."]
            });
        }
    }

    private async Task<(bool Ok, string? Value, string? Message)> NormalizeAsync(
        OperationalFieldDef def,
        string? raw,
        CancellationToken cancellationToken)
    {
        var text = OperationalFieldValues.Trim(raw);
        if (text is null)
        {
            return (true, null, null);
        }

        switch (def.Kind)
        {
            case OperationalFieldKinds.Decimal:
            {
                var value = OperationalFieldValues.ParseDecimal(text);
                if (value is null || value <= 0m)
                {
                    return (false, null, $"{def.Label} phải là số lớn hơn 0. Để trống nếu chưa có.");
                }

                return (true, OperationalFieldValues.FormatDecimal(decimal.Round(value.Value, 4, MidpointRounding.AwayFromZero)), null);
            }
            case OperationalFieldKinds.Integer:
            {
                if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value < 0
                    || (def.Code == OperationalFieldCodes.SequenceNo && value < 1))
                {
                    return (false, null, $"{def.Label} phải là số nguyên hợp lệ.");
                }

                return (true, OperationalFieldValues.FormatInt(value), null);
            }
            case OperationalFieldKinds.DateTime:
            {
                if (!DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var value))
                {
                    return (false, null, $"{def.Label} không đúng định dạng ngày giờ.");
                }

                return (true, OperationalFieldValues.FormatDate(value), null);
            }
            case OperationalFieldKinds.TransportMode:
            {
                var mode = text.ToLowerInvariant();
                return mode.Length > 32
                    ? (false, null, $"{def.Label} tối đa 32 ký tự.")
                    : (true, mode, null);
            }
            case OperationalFieldKinds.Commodity:
            {
                if (!Guid.TryParse(text, out var id))
                {
                    return (false, null, "Loại hàng không hợp lệ.");
                }

                var exists = await _db.CommodityTypes.AsNoTracking().AnyAsync(c => c.Id == id && c.IsActive, cancellationToken);
                return exists ? (true, id.ToString(), null) : (false, null, "Không tìm thấy loại hàng.");
            }
            case OperationalFieldKinds.Flags:
            {
                var flags = text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(f => f.ToLowerInvariant())
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(f => f, StringComparer.Ordinal)
                    .ToList();
                if (flags.Any(f => f.Length > 32))
                {
                    return (false, null, "Thuộc tính đặc biệt không hợp lệ.");
                }

                return (true, flags.Count == 0 ? null : string.Join(",", flags), null);
            }
            default:
                return def.MaxLength > 0 && text.Length > def.MaxLength
                    ? (false, null, $"{def.Label} tối đa {def.MaxLength} ký tự.")
                    : (true, text, null);
        }
    }

    private static string UomFor(string measureCode, string? transportMode) => measureCode switch
    {
        MeasureCodes.PackageCount => "pcs",
        MeasureCodes.GrossWeightKg => "kg",
        MeasureCodes.VolumeCbm => "cbm",
        MeasureCodes.ChargeableWeightKg => ChargeableWeightPolicy.IsSea(transportMode) ? "wm" : "kg",
        _ => "unit"
    };

    private static string AuditValue(OperationalFieldDef def, string? value, string source) =>
        JsonSerializer.Serialize(new Dictionary<string, string?>
        {
            ["field"] = def.Label,
            ["value"] = value ?? "Chưa xác định",
            ["source"] = source
        });

    private static string StaleReason(string type, string code, IEnumerable<FieldChange> changes)
    {
        var parts = changes
            .Where(c => OperationalFieldCatalog.Find(type, c.Code)?.RatingRelevant == true)
            .Select(c => $"{OperationalFieldCatalog.Label(type, c.Code)}: {c.OldValue ?? "trống"} → {c.NewValue ?? "trống"}")
            .Distinct();
        return $"{OperationalFieldCatalog.ObjectLabel(type)} {code} thay đổi — {string.Join("; ", parts)}";
    }

    private static byte[] RowVersionOf(OperationalReferenceRecord record) =>
        (byte[]?)record.Bill?.RowVersion
        ?? record.Order?.RowVersion
        ?? record.Shipment?.RowVersion
        ?? record.Leg?.RowVersion
        ?? record.Movement?.RowVersion
        ?? [];
}
