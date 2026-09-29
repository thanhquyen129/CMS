using System.Text.Json;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.Identity;
using LCMS.Domain.Entities;
using MediatR;

namespace LCMS.Application.OperationalReferences.Edit;

/// <summary>
/// "Đã xác nhận trọng lượng tính cước" — confirms the current value without a reason.
/// Confirmation ≠ Override: the value does not change, so no rating becomes stale (ADR-0039 D06).
/// </summary>
public sealed record ConfirmChargeableWeightCommand(string ObjectType, Guid ObjectId) : IRequest<ChargeableWeightStateDto>;

public sealed class ConfirmChargeableWeightCommandHandler : IRequestHandler<ConfirmChargeableWeightCommand, ChargeableWeightStateDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IPermissionService _permissions;
    private readonly ICurrentUserContext _user;
    private readonly IOrganizationHierarchyService _orgHierarchy;
    private readonly IAuditWriter _audit;

    public ConfirmChargeableWeightCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenant,
        IPermissionService permissions,
        ICurrentUserContext user,
        IOrganizationHierarchyService orgHierarchy,
        IAuditWriter audit)
    {
        _db = db;
        _tenant = tenant;
        _permissions = permissions;
        _user = user;
        _orgHierarchy = orgHierarchy;
        _audit = audit;
    }

    public async Task<ChargeableWeightStateDto> Handle(ConfirmChargeableWeightCommand request, CancellationToken cancellationToken)
    {
        if (!_tenant.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var type = OperationalReferenceRecord.NormalizeType(request.ObjectType);
        if (!OperationalObjectTypes.CargoParents.Contains(type))
        {
            throw new ConflictAppException("Trọng lượng tính cước chỉ có trên Bill, đơn hàng hoặc lô hàng.");
        }

        var record = await OperationalReferenceRecord.LoadAsync(_db, type, request.ObjectId, track: true, cancellationToken);
        var access = new OperationalReferenceAccess(_db, _permissions, _user, _orgHierarchy);
        await access.EnsureAsync(
            record,
            OperationalReferenceAccess.EditPermission(type),
            "Bạn không có quyền xác nhận trọng lượng tính cước.",
            cancellationToken);

        record.Overrides.TryGetValue(OperationalFieldCodes.ChargeableWeightKg, out var overrideRow);
        var state = ChargeableWeightPolicy.Resolve(record.Measures, overrideRow, record.TransportMode, record.SourceSystem);
        if (state.Value is null)
        {
            throw new ConflictAppException(
                $"Chưa có Trọng lượng tính cước hợp lệ để xác nhận. {state.MissingReason}".Trim());
        }

        if (state.IsConfirmed)
        {
            return state;
        }

        var now = DateTimeOffset.UtcNow;
        var measure = record.Measure(MeasureCodes.ChargeableWeightKg);
        if (measure is null)
        {
            measure = new OperationalMeasurement
            {
                TenantId = _tenant.TenantId!.Value,
                ObjectType = type,
                ObjectId = record.Id,
                MeasureCode = MeasureCodes.ChargeableWeightKg,
                Quantity = state.Value.Value,
                Uom = ChargeableWeightPolicy.IsSea(record.TransportMode) ? "wm" : "kg",
                SourceChannel = MeasureSourceChannels.System,
                RuleCode = state.RuleCode
            };
            _db.OperationalMeasurements.Add(measure);
            record.Measures.Add(measure);
            record.MirrorMeasureToContext(OperationalFieldCodes.ChargeableWeightKg, state.Value);
            record.FlushContext();
        }

        measure.IsConfirmed = true;
        measure.ConfirmedAt = now;
        _audit.Append(
            AuditActions.MeasurementConfirm,
            type,
            record.Id,
            afterJson: JsonSerializer.Serialize(new Dictionary<string, string?>
            {
                ["field"] = OperationalFieldCatalog.ChargeableWeight.Label,
                ["value"] = OperationalFieldValues.FormatDecimal(state.Value),
                ["source"] = state.SourceLabel,
                ["rule"] = state.RuleCode
            }));

        await _db.SaveChangesAsync(cancellationToken);
        return ChargeableWeightPolicy.Resolve(record.Measures, overrideRow, record.TransportMode, record.SourceSystem);
    }
}
