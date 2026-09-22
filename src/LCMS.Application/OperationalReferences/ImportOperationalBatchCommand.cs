using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.OperationalReferences;

public sealed record OperationalImportRow(
    string ObjectType,
    string BusinessNo,
    string ExternalId,
    string? OriginCode = null,
    string? DestinationCode = null,
    decimal? GrossWeightKg = null,
    decimal? ChargeableWeightKg = null,
    string? ParentExternalId = null,
    int? SequenceNo = null,
    DateTimeOffset? MovementOn = null);

public sealed record OperationalImportIssue(int Row, string Field, string Message);

public sealed record OperationalImportPreview(bool CanCommit, IReadOnlyList<OperationalImportIssue> Issues);

public sealed record PreviewOperationalImportCommand(
    string SourceSystem,
    IReadOnlyList<OperationalImportRow> Rows) : IRequest<OperationalImportPreview>;

public sealed record CommitOperationalImportCommand(
    string SourceSystem,
    IReadOnlyList<OperationalImportRow> Rows) : IRequest<int>;

public sealed class PreviewOperationalImportCommandValidator : AbstractValidator<PreviewOperationalImportCommand>
{
    public PreviewOperationalImportCommandValidator()
    {
        RuleFor(x => x.SourceSystem).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Rows).NotNull();
    }
}

public sealed class CommitOperationalImportCommandValidator : AbstractValidator<CommitOperationalImportCommand>
{
    public CommitOperationalImportCommandValidator()
    {
        RuleFor(x => x.SourceSystem).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Rows).NotNull();
    }
}

public sealed class PreviewOperationalImportCommandHandler
    : IRequestHandler<PreviewOperationalImportCommand, OperationalImportPreview>
{
    private readonly OperationalImportBatch _batch;

    public PreviewOperationalImportCommandHandler(OperationalImportBatch batch) => _batch = batch;

    public async Task<OperationalImportPreview> Handle(
        PreviewOperationalImportCommand request,
        CancellationToken cancellationToken)
    {
        var issues = await _batch.ValidateAsync(request.SourceSystem, request.Rows, cancellationToken);
        return new OperationalImportPreview(issues.Count == 0, issues);
    }
}

public sealed class CommitOperationalImportCommandHandler : IRequestHandler<CommitOperationalImportCommand, int>
{
    private readonly OperationalImportBatch _batch;

    public CommitOperationalImportCommandHandler(OperationalImportBatch batch) => _batch = batch;

    public Task<int> Handle(CommitOperationalImportCommand request, CancellationToken cancellationToken) =>
        _batch.CommitAsync(request.SourceSystem, request.Rows, cancellationToken);
}

/// <summary>Preview and all-or-nothing commit for operational import rows.</summary>
public sealed class OperationalImportBatch
{
    private static readonly HashSet<string> Types = new(StringComparer.Ordinal)
    {
        OperationalObjectTypes.Bill,
        OperationalObjectTypes.Order,
        OperationalObjectTypes.Shipment,
        OperationalObjectTypes.Leg,
        OperationalObjectTypes.Movement
    };

    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenant;

    public OperationalImportBatch(ILcmsDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<OperationalImportIssue>> ValidateAsync(
        string sourceSystem,
        IReadOnlyList<OperationalImportRow> rows,
        CancellationToken cancellationToken)
    {
        if (!_tenant.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var source = sourceSystem.Trim();
        var issues = new List<OperationalImportIssue>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var shipmentExternals = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < rows.Count; i++)
        {
            if (Normalize(rows[i].ObjectType) == OperationalObjectTypes.Shipment
                && !string.IsNullOrWhiteSpace(rows[i].ExternalId))
            {
                shipmentExternals.Add(rows[i].ExternalId.Trim());
            }
        }

        for (var i = 0; i < rows.Count; i++)
        {
            var rowNo = i + 1;
            var row = rows[i];
            var type = Normalize(row.ObjectType);
            if (!Types.Contains(type))
            {
                issues.Add(new OperationalImportIssue(rowNo, "objectType", "Loại đối tượng không hợp lệ."));
                continue;
            }

            if (string.IsNullOrWhiteSpace(row.BusinessNo))
            {
                issues.Add(new OperationalImportIssue(rowNo, "businessNo", "Thiếu số nghiệp vụ."));
            }

            if (string.IsNullOrWhiteSpace(row.ExternalId))
            {
                issues.Add(new OperationalImportIssue(rowNo, "externalId", "Thiếu mã ngoài."));
                continue;
            }

            var external = row.ExternalId.Trim();
            if (!seen.Add(type + "|" + external))
            {
                issues.Add(new OperationalImportIssue(rowNo, "externalId", "Mã ngoài bị trùng trong tệp."));
            }

            var origin = Trim(row.OriginCode);
            var destination = Trim(row.DestinationCode);
            if (origin is not null && destination is not null
                && string.Equals(origin, destination, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new OperationalImportIssue(rowNo, "destinationCode", "Điểm đi và điểm đến không được trùng."));
            }

            if (type == OperationalObjectTypes.Leg)
            {
                var parent = Trim(row.ParentExternalId);
                if (parent is null)
                {
                    issues.Add(new OperationalImportIssue(rowNo, "parentExternalId", "Chặng vận chuyển cần mã Shipment."));
                }
                else if (!shipmentExternals.Contains(parent))
                {
                    var exists = await _db.Shipments.AnyAsync(
                        s => s.SourceSystem == source && s.ExternalId == parent,
                        cancellationToken);
                    if (!exists)
                    {
                        issues.Add(new OperationalImportIssue(rowNo, "parentExternalId", "Không tìm thấy Shipment theo mã ngoài."));
                    }
                }
            }

            if (row.ChargeableWeightKg is decimal weight)
            {
                var current = await FindParentIdAsync(type, source, external, cancellationToken);
                if (current is Guid parentId)
                {
                    var locked = await _db.OperationalMeasurements.AsNoTracking().FirstOrDefaultAsync(
                        m => m.ObjectType == type
                             && m.ObjectId == parentId
                             && m.MeasureCode == MeasureCodes.ChargeableWeightKg
                             && m.IsConfirmed,
                        cancellationToken);
                    if (locked is not null && locked.Quantity != weight)
                    {
                        issues.Add(new OperationalImportIssue(
                            rowNo,
                            "chargeableWeightKg",
                            "Trọng lượng tính cước đã xác nhận. Nhập lý do để ghi đè."));
                    }
                }
            }
        }

        return issues;
    }

    public async Task<int> CommitAsync(
        string sourceSystem,
        IReadOnlyList<OperationalImportRow> rows,
        CancellationToken cancellationToken)
    {
        var issues = await ValidateAsync(sourceSystem, rows, cancellationToken);
        if (issues.Count > 0)
        {
            throw new ConflictAppException("Tệp nhập có lỗi. Không ghi dữ liệu.");
        }

        var tenantId = _tenant.TenantId!.Value;
        var source = sourceSystem.Trim();
        var shipments = new Dictionary<string, Shipment>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            var type = Normalize(row.ObjectType);
            if (type is OperationalObjectTypes.Leg)
            {
                continue;
            }

            var id = await UpsertParentAsync(type, source, tenantId, row, shipments, cancellationToken);
            await WriteMeasuresAsync(type, id, tenantId, source, row, cancellationToken);
        }

        foreach (var row in rows)
        {
            if (Normalize(row.ObjectType) != OperationalObjectTypes.Leg)
            {
                continue;
            }

            var parentExternal = row.ParentExternalId!.Trim();
            if (!shipments.TryGetValue(parentExternal, out var shipment))
            {
                shipment = await _db.Shipments.FirstAsync(
                    s => s.SourceSystem == source && s.ExternalId == parentExternal,
                    cancellationToken);
            }

            var external = row.ExternalId.Trim();
            var leg = await _db.TransportLegs.FirstOrDefaultAsync(
                l => l.SourceSystem == source && l.ExternalId == external,
                cancellationToken);
            if (leg is null)
            {
                leg = new TransportLeg
                {
                    TenantId = tenantId,
                    ShipmentId = shipment.Id,
                    LegNo = row.BusinessNo.Trim(),
                    SourceSystem = source,
                    ExternalId = external,
                    OperationalStatus = "draft",
                    IsActive = true,
                    SequenceNo = row.SequenceNo ?? 1
                };
                _db.TransportLegs.Add(leg);
            }

            leg.OriginCode = Trim(row.OriginCode) ?? leg.OriginCode;
            leg.DestinationCode = Trim(row.DestinationCode) ?? leg.DestinationCode;
            leg.SequenceNo = row.SequenceNo ?? leg.SequenceNo;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return rows.Count;
    }

    private async Task<Guid> UpsertParentAsync(
        string type,
        string source,
        Guid tenantId,
        OperationalImportRow row,
        Dictionary<string, Shipment> shipments,
        CancellationToken cancellationToken)
    {
        var external = row.ExternalId.Trim();
        var businessNo = row.BusinessNo.Trim();
        var origin = Trim(row.OriginCode);
        var destination = Trim(row.DestinationCode);

        if (type == OperationalObjectTypes.Bill)
        {
            var bill = await _db.Bills.FirstOrDefaultAsync(
                b => b.SourceSystem == source && b.ExternalId == external,
                cancellationToken);
            if (bill is null)
            {
                bill = new Bill
                {
                    TenantId = tenantId,
                    BillNo = businessNo,
                    BillType = "house",
                    SourceSystem = source,
                    ExternalId = external,
                    OperationalStatus = "draft",
                    IsActive = true,
                    BillDate = DateTimeOffset.UtcNow
                };
                _db.Bills.Add(bill);
            }

            bill.OriginCode = origin ?? bill.OriginCode;
            bill.DestinationCode = destination ?? bill.DestinationCode;
            await OwnAsync(type, bill.Id, tenantId, source, origin, cancellationToken);
            return bill.Id;
        }

        if (type == OperationalObjectTypes.Order)
        {
            var order = await _db.Orders.FirstOrDefaultAsync(
                o => o.SourceSystem == source && o.ExternalId == external,
                cancellationToken);
            if (order is null)
            {
                order = new Order
                {
                    TenantId = tenantId,
                    OrderNo = businessNo,
                    SourceSystem = source,
                    ExternalId = external,
                    OperationalStatus = "draft",
                    IsActive = true,
                    OrderDate = DateTimeOffset.UtcNow
                };
                _db.Orders.Add(order);
            }

            order.OriginCode = origin ?? order.OriginCode;
            order.DestinationCode = destination ?? order.DestinationCode;
            await OwnAsync(type, order.Id, tenantId, source, origin, cancellationToken);
            return order.Id;
        }

        if (type == OperationalObjectTypes.Shipment)
        {
            var shipment = await _db.Shipments.FirstOrDefaultAsync(
                s => s.SourceSystem == source && s.ExternalId == external,
                cancellationToken);
            if (shipment is null)
            {
                shipment = new Shipment
                {
                    TenantId = tenantId,
                    ShipmentNo = businessNo,
                    SourceSystem = source,
                    ExternalId = external,
                    OperationalStatus = "draft",
                    IsActive = true
                };
                _db.Shipments.Add(shipment);
            }

            shipment.OriginCode = origin ?? shipment.OriginCode;
            shipment.DestinationCode = destination ?? shipment.DestinationCode;
            shipments[external] = shipment;
            await OwnAsync(type, shipment.Id, tenantId, source, origin, cancellationToken);
            return shipment.Id;
        }

        var movement = await _db.TransportMovements.FirstOrDefaultAsync(
            m => m.SourceSystem == source && m.ExternalId == external,
            cancellationToken);
        if (movement is null)
        {
            movement = new TransportMovement
            {
                TenantId = tenantId,
                MovementNo = businessNo,
                SourceSystem = source,
                ExternalId = external,
                OperationalStatus = "draft",
                IsActive = true,
                MovementOn = row.MovementOn
            };
            _db.TransportMovements.Add(movement);
        }

        movement.MovementOn = row.MovementOn ?? movement.MovementOn;
        return movement.Id;
    }

    private async Task WriteMeasuresAsync(
        string type,
        Guid objectId,
        Guid tenantId,
        string source,
        OperationalImportRow row,
        CancellationToken cancellationToken)
    {
        await UpsertMeasureAsync(type, objectId, tenantId, source, MeasureCodes.GrossWeightKg, row.GrossWeightKg, "kg", cancellationToken);
        await UpsertMeasureAsync(type, objectId, tenantId, source, MeasureCodes.ChargeableWeightKg, row.ChargeableWeightKg, "kg", cancellationToken);
    }

    private async Task UpsertMeasureAsync(
        string type,
        Guid objectId,
        Guid tenantId,
        string source,
        string code,
        decimal? value,
        string uom,
        CancellationToken cancellationToken)
    {
        if (value is null || !OperationalObjectTypes.CargoParents.Contains(type))
        {
            return;
        }

        var existing = await _db.OperationalMeasurements.FirstOrDefaultAsync(
            m => m.ObjectType == type && m.ObjectId == objectId && m.MeasureCode == code,
            cancellationToken);
        if (existing is null)
        {
            _db.OperationalMeasurements.Add(new OperationalMeasurement
            {
                TenantId = tenantId,
                ObjectType = type,
                ObjectId = objectId,
                MeasureCode = code,
                Quantity = value.Value,
                Uom = uom,
                SourceChannel = "import"
            });
            return;
        }

        if (existing.IsConfirmed)
        {
            return;
        }

        existing.Quantity = value.Value;
        existing.Uom = uom;
        _ = source;
    }

    private async Task OwnAsync(
        string type,
        Guid objectId,
        Guid tenantId,
        string source,
        string? origin,
        CancellationToken cancellationToken)
    {
        if (origin is null || string.Equals(source, OperationalSourceSystems.LcmsManual, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var exists = await _db.FieldOwnerships.AnyAsync(
            f => f.ObjectType == type && f.ObjectId == objectId && f.FieldName == "origin_code",
            cancellationToken);
        if (exists)
        {
            return;
        }

        _db.FieldOwnerships.Add(new FieldOwnership
        {
            TenantId = tenantId,
            ObjectType = type,
            ObjectId = objectId,
            FieldName = "origin_code",
            OwnerSystem = source
        });
    }

    private async Task<Guid?> FindParentIdAsync(
        string type,
        string source,
        string external,
        CancellationToken cancellationToken)
    {
        if (type == OperationalObjectTypes.Bill)
        {
            return await _db.Bills.Where(b => b.SourceSystem == source && b.ExternalId == external)
                .Select(b => (Guid?)b.Id).FirstOrDefaultAsync(cancellationToken);
        }

        if (type == OperationalObjectTypes.Order)
        {
            return await _db.Orders.Where(o => o.SourceSystem == source && o.ExternalId == external)
                .Select(o => (Guid?)o.Id).FirstOrDefaultAsync(cancellationToken);
        }

        if (type == OperationalObjectTypes.Shipment)
        {
            return await _db.Shipments.Where(s => s.SourceSystem == source && s.ExternalId == external)
                .Select(s => (Guid?)s.Id).FirstOrDefaultAsync(cancellationToken);
        }

        return null;
    }

    private static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
