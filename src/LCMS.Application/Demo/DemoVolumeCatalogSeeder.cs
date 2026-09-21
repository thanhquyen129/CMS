using LCMS.Application.Abstractions;
using LCMS.Application.OperationalReferences;
using LCMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LCMS.Application.Demo;

/// <summary>
/// Idempotent volume catalog (≥120 rows per list type) for UI pagination / filter testing.
/// Prefix <c>VOL-</c>; re-run fills missing codes only. Does not invent extra Cost/Revenue from documents.
/// </summary>
public sealed class DemoVolumeCatalogSeeder
{
    public const int TargetCount = 120;
    public const string SourceSystem = "demo-volume";

    private static readonly (string Origin, string Dest, string Route)[] Lanes =
    [
        ("SGN", "LAX", "SGN-LAX"),
        ("HAN", "ICN", "HAN-ICN"),
        ("HPH", "RTM", "HPH-RTM"),
        ("DAD", "SIN", "DAD-SIN"),
        ("CXR", "BKK", "CXR-BKK"),
        ("PQC", "KUL", "PQC-KUL"),
        ("VCA", "NRT", "VCA-NRT"),
        ("HUI", "FRA", "HUI-FRA")
    ];

    private static readonly string[] Modes = ["air", "sea", "road", "rail"];

    private readonly ILcmsDbContext _db;
    private readonly ILogger<DemoVolumeCatalogSeeder> _logger;

    public DemoVolumeCatalogSeeder(ILcmsDbContext db, ILogger<DemoVolumeCatalogSeeder> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<DemoVolumeResult> EnsureAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var now = DateTimeOffset.UtcNow;
        var orgId = await EnsureOrgAsync(tenantId, cancellationToken);

        var customerIds = await EnsurePartiesAsync(tenantId, customer: true, cancellationToken);
        var vendorIds = await EnsurePartiesAsync(tenantId, customer: false, cancellationToken);
        await EnsureCatalogLookupsAsync(tenantId, cancellationToken);

        var orderIds = await EnsureOrdersAsync(tenantId, customerIds, now, cancellationToken);
        var billIds = await EnsureBillsAsync(tenantId, orgId, customerIds, now, cancellationToken);
        var shipmentIds = await EnsureShipmentsAsync(tenantId, now, cancellationToken);
        var legIds = await EnsureLegsAsync(tenantId, shipmentIds, cancellationToken);
        var movementIds = await EnsureMovementsAsync(tenantId, cancellationToken);

        await EnsureLinksAsync(tenantId, orderIds, billIds, shipmentIds, legIds, movementIds, cancellationToken);
        await EnsureCostsAsync(tenantId, billIds, vendorIds, today, now, cancellationToken);
        await EnsureRevenuesAsync(tenantId, billIds, customerIds, today, now, cancellationToken);
        await EnsureDocumentsAsync(tenantId, billIds, vendorIds, customerIds, today, now, cancellationToken);
        await EnsureApArAsync(tenantId, billIds, vendorIds, customerIds, today, now, cancellationToken);
        await EnsureSettlementsAsync(tenantId, billIds, vendorIds, customerIds, today, cancellationToken);
        await EnsureRateCardsAsync(tenantId, now, cancellationToken);
        await EnsureBankFeedAsync(tenantId, today, cancellationToken);

        var counts = await CountAsync(tenantId, cancellationToken);
        var summary = "Dữ liệu mẫu VOL: " + string.Join(", ", counts.Select(kv => $"{kv.Key}={kv.Value}"));
        _logger.LogInformation("Volume seed tenant {TenantId}: {Summary}", tenantId, summary);
        var skipped = counts.Values.All(v => v >= TargetCount);
        return new DemoVolumeResult(skipped, summary, counts);
    }

    public async Task<IReadOnlyDictionary<string, int>> CountAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var tid = tenantId;
        return new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["customer"] = await CountPartiesAsync(tid, "VOL-CUS-", cancellationToken),
            ["vendor"] = await CountPartiesAsync(tid, "VOL-VND-", cancellationToken),
            ["order"] = await _db.Orders.IgnoreQueryFilters().CountAsync(o => o.TenantId == tid && o.DeletedAt == null && o.OrderNo.StartsWith("VOL-ORD-"), cancellationToken),
            ["bill"] = await _db.Bills.IgnoreQueryFilters().CountAsync(b => b.TenantId == tid && b.DeletedAt == null && b.BillNo.StartsWith("VOL-BILL-"), cancellationToken),
            ["shipment"] = await _db.Shipments.IgnoreQueryFilters().CountAsync(s => s.TenantId == tid && s.DeletedAt == null && s.ShipmentNo.StartsWith("VOL-SHP-"), cancellationToken),
            ["leg"] = await _db.TransportLegs.IgnoreQueryFilters().CountAsync(l => l.TenantId == tid && l.DeletedAt == null && l.LegNo.StartsWith("VOL-LEG-"), cancellationToken),
            ["movement"] = await _db.TransportMovements.IgnoreQueryFilters().CountAsync(m => m.TenantId == tid && m.DeletedAt == null && m.MovementNo.StartsWith("VOL-MOV-"), cancellationToken),
            ["cost"] = await _db.Costs.IgnoreQueryFilters().CountAsync(c => c.TenantId == tid && c.DeletedAt == null && c.CostTypeCode != null && c.CostTypeCode.StartsWith("VOL-COST-"), cancellationToken),
            ["revenue"] = await _db.Revenues.IgnoreQueryFilters().CountAsync(r => r.TenantId == tid && r.DeletedAt == null && r.RevenueTypeCode != null && r.RevenueTypeCode.StartsWith("VOL-REV-"), cancellationToken),
            ["document"] = await _db.FinancialDocuments.IgnoreQueryFilters().CountAsync(d => d.TenantId == tid && d.DeletedAt == null && d.DocumentNo.StartsWith("VOL-DOC-"), cancellationToken),
            ["ap"] = await _db.AccountsPayable.IgnoreQueryFilters().CountAsync(a => a.TenantId == tid && a.DeletedAt == null && a.Notes != null && a.Notes.StartsWith("VOL-AP-"), cancellationToken),
            ["ar"] = await _db.AccountsReceivable.IgnoreQueryFilters().CountAsync(a => a.TenantId == tid && a.DeletedAt == null && a.Notes != null && a.Notes.StartsWith("VOL-AR-"), cancellationToken),
            ["payment"] = await _db.Payments.IgnoreQueryFilters().CountAsync(p => p.TenantId == tid && p.DeletedAt == null && p.ReferenceNo != null && p.ReferenceNo.StartsWith("VOL-PAY-"), cancellationToken),
            ["collection"] = await _db.Collections.IgnoreQueryFilters().CountAsync(c => c.TenantId == tid && c.DeletedAt == null && c.ReferenceNo != null && c.ReferenceNo.StartsWith("VOL-COL-"), cancellationToken),
            ["rateCard"] = await _db.RateCards.IgnoreQueryFilters().CountAsync(r => r.TenantId == tid && r.DeletedAt == null && r.Code.StartsWith("VOL-RC-"), cancellationToken),
            ["bankFeed"] = await _db.BankFeedLines.IgnoreQueryFilters().CountAsync(b => b.TenantId == tid && b.DeletedAt == null && b.BankReference != null && b.BankReference.StartsWith("VOL-BANK-"), cancellationToken),
            ["location"] = await CountCatalogAsync(tid, MasterCatalogKinds.Location, "VOL-LOC-", cancellationToken),
            ["route"] = await CountCatalogAsync(tid, MasterCatalogKinds.TransportRoute, "VOL-RTE-", cancellationToken)
        };
    }

    private async Task<Guid> EnsureOrgAsync(Guid tid, CancellationToken ct)
    {
        var org = await _db.Organizations.IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.TenantId == tid && o.DeletedAt == null && (o.Code == "DEMO-ORG" || o.Code == "VOL-ORG"), ct);
        if (org is not null)
        {
            return org.Id;
        }

        org = new Organization { TenantId = tid, Code = "VOL-ORG", Name = "Chi nhánh mẫu HCM", IsActive = true };
        _db.Organizations.Add(org);
        await _db.SaveChangesAsync(ct);
        return org.Id;
    }

    private async Task<Guid[]> EnsurePartiesAsync(Guid tid, bool customer, CancellationToken ct)
    {
        var prefix = customer ? "VOL-CUS-" : "VOL-VND-";
        var role = customer ? PartyRoleCodes.Customer : PartyRoleCodes.Vendor;
        var side = customer ? PartyRoleCodes.Payer : PartyRoleCodes.Payee;
        var ids = new Guid[TargetCount + 1];
        var existing = await _db.BusinessParties.IgnoreQueryFilters()
            .Where(p => p.TenantId == tid && p.DeletedAt == null && p.Code.StartsWith(prefix))
            .Select(p => new { p.Id, p.Code })
            .ToListAsync(ct);
        foreach (var row in existing)
        {
            if (TryIndex(row.Code, prefix, out var i))
            {
                ids[i] = row.Id;
            }
        }

        var added = new List<BusinessParty>();
        for (var i = 1; i <= TargetCount; i++)
        {
            if (ids[i] != Guid.Empty)
            {
                continue;
            }

            var lane = Lanes[(i - 1) % Lanes.Length];
            var party = new BusinessParty
            {
                TenantId = tid,
                Code = Code(prefix, i),
                Name = customer
                    ? $"Khách hàng mẫu {i:0000} ({lane.Route})"
                    : $"Nhà cung cấp mẫu {i:0000} ({lane.Route})",
                LegalName = customer
                    ? $"Công ty CP Khách hàng mẫu {i:0000}"
                    : $"Công ty TNHH NCC mẫu {i:0000}",
                TaxId = customer ? $"010{i:0000000}" : $"030{i:0000000}",
                Phone = customer ? $"0901{i:000000}" : $"0903{i:000000}",
                Email = customer ? $"vol.cus.{i:0000}@demo.local" : $"vol.vnd.{i:0000}@demo.local",
                CountryCode = "VN",
                DefaultCurrencyCode = "VND",
                PaymentTermDays = customer ? 30 : 15,
                CreditLimit = customer ? 2_000_000_000m : 800_000_000m,
                CreditLimitCurrencyCode = "VND",
                IsActive = i % 17 != 0,
                PartyKind = PartyKinds.Organization,
                CreditControlMode = PartyCreditControlModes.Advisory
            };
            ids[i] = party.Id;
            added.Add(party);
        }

        if (added.Count > 0)
        {
            _db.BusinessParties.AddRange(added);
            await _db.SaveChangesAsync(ct);
            var roles = new List<PartyRole>(added.Count * 2);
            foreach (var p in added)
            {
                roles.Add(new PartyRole { TenantId = tid, PartyId = p.Id, RoleCode = role, IsActive = true });
                roles.Add(new PartyRole { TenantId = tid, PartyId = p.Id, RoleCode = side, IsActive = true });
            }

            _db.PartyRoles.AddRange(roles);
            await _db.SaveChangesAsync(ct);
        }

        return ids;
    }

    private async Task EnsureCatalogLookupsAsync(Guid tid, CancellationToken ct)
    {
        var existingLoc = await CatalogCodesAsync(tid, MasterCatalogKinds.Location, "VOL-LOC-", ct);
        var existingRte = await CatalogCodesAsync(tid, MasterCatalogKinds.TransportRoute, "VOL-RTE-", ct);
        var add = new List<MasterCatalogItem>();
        for (var i = 1; i <= TargetCount; i++)
        {
            var locCode = Code("VOL-LOC-", i);
            if (!existingLoc.Contains(locCode))
            {
                var lane = Lanes[(i - 1) % Lanes.Length];
                add.Add(new MasterCatalogItem
                {
                    TenantId = tid,
                    Kind = MasterCatalogKinds.Location,
                    Code = locCode,
                    Name = i % 2 == 0 ? $"Điểm đến {lane.Dest} #{i:0000}" : $"Điểm đi {lane.Origin} #{i:0000}",
                    IsActive = true,
                    SortOrder = i,
                    AttributesJson = i % 2 == 0
                        ? $"{{\"iata\":\"{lane.Dest}\"}}"
                        : $"{{\"iata\":\"{lane.Origin}\"}}"
                });
            }

            var rteCode = Code("VOL-RTE-", i);
            if (!existingRte.Contains(rteCode))
            {
                var lane = Lanes[(i - 1) % Lanes.Length];
                add.Add(new MasterCatalogItem
                {
                    TenantId = tid,
                    Kind = MasterCatalogKinds.TransportRoute,
                    Code = rteCode,
                    Name = $"{lane.Origin} → {lane.Dest} #{i:0000}",
                    IsActive = true,
                    SortOrder = i,
                    AttributesJson = $"{{\"origin\":\"{lane.Origin}\",\"destination\":\"{lane.Dest}\"}}"
                });
            }
        }

        if (add.Count > 0)
        {
            _db.MasterCatalogItems.AddRange(add);
            await _db.SaveChangesAsync(ct);
        }
    }

    private async Task<Guid[]> EnsureOrdersAsync(Guid tid, Guid[] customerIds, DateTimeOffset now, CancellationToken ct)
    {
        const string prefix = "VOL-ORD-";
        var ids = MapToIndex(
            await _db.Orders.IgnoreQueryFilters()
                .Where(o => o.TenantId == tid && o.DeletedAt == null && o.OrderNo.StartsWith(prefix))
                .Select(o => new IdCode(o.Id, o.OrderNo))
                .ToListAsync(ct),
            prefix);
        var add = new List<Order>();
        for (var i = 1; i <= TargetCount; i++)
        {
            if (ids[i] != Guid.Empty)
            {
                continue;
            }

            var lane = Lanes[(i - 1) % Lanes.Length];
            var mode = Modes[(i - 1) % Modes.Length];
            var created = now.AddDays(-i);
            var order = new Order
            {
                TenantId = tid,
                OrderNo = Code(prefix, i),
                SourceSystem = SourceSystem,
                ExternalId = Code(prefix, i),
                OperationalStatus = i % 11 == 0 ? "draft" : "active",
                IsActive = true,
                CustomerPartyId = customerIds[i],
                TransportMode = mode,
                OriginCode = lane.Origin,
                DestinationCode = lane.Dest,
                RouteCode = $"{lane.Origin} → {lane.Dest}",
                EtdAt = created.AddDays(3),
                EtaAt = created.AddDays(10),
                CustomerReference = $"PO-{i:0000}",
                Description = $"Đơn mẫu {mode} {lane.Route}",
                ContextJson = OperationalContextJson.Serialize(SampleContext(i, lane, mode, customer: true))
            };
            order.CreatedAt = created;
            ids[i] = order.Id;
            add.Add(order);
        }

        if (add.Count > 0)
        {
            _db.Orders.AddRange(add);
            await _db.SaveChangesAsync(ct);
        }

        return ids;
    }

    private async Task<Guid[]> EnsureBillsAsync(
        Guid tid, Guid orgId, Guid[] customerIds, DateTimeOffset now, CancellationToken ct)
    {
        const string prefix = "VOL-BILL-";
        var ids = MapToIndex(
            await _db.Bills.IgnoreQueryFilters()
                .Where(b => b.TenantId == tid && b.DeletedAt == null && b.BillNo.StartsWith(prefix))
                .Select(b => new IdCode(b.Id, b.BillNo))
                .ToListAsync(ct),
            prefix);
        var add = new List<Bill>();
        for (var i = 1; i <= TargetCount; i++)
        {
            if (ids[i] != Guid.Empty)
            {
                continue;
            }

            var lane = Lanes[(i - 1) % Lanes.Length];
            var mode = Modes[(i - 1) % Modes.Length];
            var created = now.AddDays(-i);
            var bill = new Bill
            {
                TenantId = tid,
                BillNo = Code(prefix, i),
                BillType = i % 5 == 0 ? "master" : "house",
                SourceSystem = SourceSystem,
                ExternalId = Code(prefix, i),
                OperationalStatus = i % 13 == 0 ? "draft" : "active",
                IsActive = true,
                OrganizationId = orgId,
                CustomerPartyId = customerIds[i],
                RouteCode = $"{lane.Origin} → {lane.Dest}",
                EtdAt = created.AddDays(3),
                EtaAt = created.AddDays(10),
                Description = $"Bill mẫu {mode} {lane.Route}",
                TransportMode = mode,
                OriginCode = lane.Origin,
                DestinationCode = lane.Dest,
                CustomerReference = $"PO-{i:0000}",
                ContextJson = OperationalContextJson.Serialize(SampleContext(i, lane, mode, customer: true))
            };
            bill.CreatedAt = created;
            ids[i] = bill.Id;
            add.Add(bill);
        }

        if (add.Count > 0)
        {
            _db.Bills.AddRange(add);
            await _db.SaveChangesAsync(ct);
        }

        return ids;
    }

    private async Task<Guid[]> EnsureShipmentsAsync(Guid tid, DateTimeOffset now, CancellationToken ct)
    {
        const string prefix = "VOL-SHP-";
        var ids = MapToIndex(
            await _db.Shipments.IgnoreQueryFilters()
                .Where(s => s.TenantId == tid && s.DeletedAt == null && s.ShipmentNo.StartsWith(prefix))
                .Select(s => new IdCode(s.Id, s.ShipmentNo))
                .ToListAsync(ct),
            prefix);
        var add = new List<Shipment>();
        for (var i = 1; i <= TargetCount; i++)
        {
            if (ids[i] != Guid.Empty)
            {
                continue;
            }

            var lane = Lanes[(i - 1) % Lanes.Length];
            var mode = Modes[(i - 1) % Modes.Length];
            var created = now.AddDays(-i);
            var row = new Shipment
            {
                TenantId = tid,
                ShipmentNo = Code(prefix, i),
                SourceSystem = SourceSystem,
                ExternalId = Code(prefix, i),
                OperationalStatus = "active",
                IsActive = true,
                TransportMode = mode,
                OriginCode = lane.Origin,
                DestinationCode = lane.Dest,
                RouteCode = $"{lane.Origin} → {lane.Dest}",
                EtdAt = created.AddDays(2),
                EtaAt = created.AddDays(9),
                CustomerReference = $"BK-{i:0000}",
                Description = $"Shipment mẫu {lane.Route}",
                ContextJson = OperationalContextJson.Serialize(SampleContext(i, lane, mode, customer: false))
            };
            row.CreatedAt = created;
            ids[i] = row.Id;
            add.Add(row);
        }

        if (add.Count > 0)
        {
            _db.Shipments.AddRange(add);
            await _db.SaveChangesAsync(ct);
        }

        return ids;
    }

    private async Task<Guid[]> EnsureLegsAsync(Guid tid, Guid[] shipmentIds, CancellationToken ct)
    {
        const string prefix = "VOL-LEG-";
        var ids = MapToIndex(
            await _db.TransportLegs.IgnoreQueryFilters()
                .Where(l => l.TenantId == tid && l.DeletedAt == null && l.LegNo.StartsWith(prefix))
                .Select(l => new IdCode(l.Id, l.LegNo))
                .ToListAsync(ct),
            prefix);
        var add = new List<TransportLeg>();
        for (var i = 1; i <= TargetCount; i++)
        {
            if (ids[i] != Guid.Empty || shipmentIds[i] == Guid.Empty)
            {
                continue;
            }

            var row = new TransportLeg
            {
                TenantId = tid,
                LegNo = Code(prefix, i),
                ShipmentId = shipmentIds[i],
                SourceSystem = SourceSystem,
                ExternalId = Code(prefix, i),
                OperationalStatus = "active",
                IsActive = true
            };
            ids[i] = row.Id;
            add.Add(row);
        }

        if (add.Count > 0)
        {
            _db.TransportLegs.AddRange(add);
            await _db.SaveChangesAsync(ct);
        }

        return ids;
    }

    private async Task<Guid[]> EnsureMovementsAsync(Guid tid, CancellationToken ct)
    {
        const string prefix = "VOL-MOV-";
        var ids = MapToIndex(
            await _db.TransportMovements.IgnoreQueryFilters()
                .Where(m => m.TenantId == tid && m.DeletedAt == null && m.MovementNo.StartsWith(prefix))
                .Select(m => new IdCode(m.Id, m.MovementNo))
                .ToListAsync(ct),
            prefix);
        var add = new List<TransportMovement>();
        for (var i = 1; i <= TargetCount; i++)
        {
            if (ids[i] != Guid.Empty)
            {
                continue;
            }

            var row = new TransportMovement
            {
                TenantId = tid,
                MovementNo = Code(prefix, i),
                SourceSystem = SourceSystem,
                ExternalId = Code(prefix, i),
                OperationalStatus = "active",
                IsActive = true
            };
            ids[i] = row.Id;
            add.Add(row);
        }

        if (add.Count > 0)
        {
            _db.TransportMovements.AddRange(add);
            await _db.SaveChangesAsync(ct);
        }

        return ids;
    }

    private async Task EnsureLinksAsync(
        Guid tid,
        Guid[] orderIds,
        Guid[] billIds,
        Guid[] shipmentIds,
        Guid[] legIds,
        Guid[] movementIds,
        CancellationToken ct)
    {
        var orderSet = (await _db.OrderBillLinks.IgnoreQueryFilters()
            .Where(l => l.TenantId == tid && l.DeletedAt == null)
            .Select(l => new { l.OrderId, l.BillId })
            .ToListAsync(ct))
            .Select(l => l.OrderId + ":" + l.BillId)
            .ToHashSet(StringComparer.Ordinal);
        var shipSet = (await _db.BillShipmentLinks.IgnoreQueryFilters()
            .Where(l => l.TenantId == tid && l.DeletedAt == null)
            .Select(l => new { l.BillId, l.ShipmentId })
            .ToListAsync(ct))
            .Select(l => l.BillId + ":" + l.ShipmentId)
            .ToHashSet(StringComparer.Ordinal);
        var legSet = (await _db.BillLegLinks.IgnoreQueryFilters()
            .Where(l => l.TenantId == tid && l.DeletedAt == null)
            .Select(l => new { l.BillId, l.TransportLegId })
            .ToListAsync(ct))
            .Select(l => l.BillId + ":" + l.TransportLegId)
            .ToHashSet(StringComparer.Ordinal);
        var movSet = (await _db.BillMovementLinks.IgnoreQueryFilters()
            .Where(l => l.TenantId == tid && l.DeletedAt == null)
            .Select(l => new { l.BillId, l.TransportMovementId })
            .ToListAsync(ct))
            .Select(l => l.BillId + ":" + l.TransportMovementId)
            .ToHashSet(StringComparer.Ordinal);
        var lmSet = (await _db.LegMovementLinks.IgnoreQueryFilters()
            .Where(l => l.TenantId == tid && l.DeletedAt == null)
            .Select(l => new { l.TransportLegId, l.TransportMovementId })
            .ToListAsync(ct))
            .Select(l => l.TransportLegId + ":" + l.TransportMovementId)
            .ToHashSet(StringComparer.Ordinal);

        for (var i = 1; i <= TargetCount; i++)
        {
            if (orderIds[i] != Guid.Empty && billIds[i] != Guid.Empty
                && orderSet.Add(orderIds[i] + ":" + billIds[i]))
            {
                _db.OrderBillLinks.Add(new OrderBillLink
                {
                    TenantId = tid,
                    OrderId = orderIds[i],
                    BillId = billIds[i]
                });
            }

            if (billIds[i] != Guid.Empty && shipmentIds[i] != Guid.Empty
                && shipSet.Add(billIds[i] + ":" + shipmentIds[i]))
            {
                _db.BillShipmentLinks.Add(new BillShipmentLink
                {
                    TenantId = tid,
                    BillId = billIds[i],
                    ShipmentId = shipmentIds[i]
                });
            }

            if (billIds[i] != Guid.Empty && legIds[i] != Guid.Empty
                && legSet.Add(billIds[i] + ":" + legIds[i]))
            {
                _db.BillLegLinks.Add(new BillLegLink
                {
                    TenantId = tid,
                    BillId = billIds[i],
                    TransportLegId = legIds[i]
                });
            }

            if (billIds[i] != Guid.Empty && movementIds[i] != Guid.Empty
                && movSet.Add(billIds[i] + ":" + movementIds[i]))
            {
                _db.BillMovementLinks.Add(new BillMovementLink
                {
                    TenantId = tid,
                    BillId = billIds[i],
                    TransportMovementId = movementIds[i]
                });
            }

            if (legIds[i] != Guid.Empty && movementIds[i] != Guid.Empty
                && lmSet.Add(legIds[i] + ":" + movementIds[i]))
            {
                _db.LegMovementLinks.Add(new LegMovementLink
                {
                    TenantId = tid,
                    TransportLegId = legIds[i],
                    TransportMovementId = movementIds[i]
                });
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task EnsureCostsAsync(
        Guid tid, Guid[] billIds, Guid[] vendorIds, DateOnly today, DateTimeOffset now, CancellationToken ct)
    {
        var existing = await _db.Costs.IgnoreQueryFilters()
            .Where(c => c.TenantId == tid && c.DeletedAt == null && c.CostTypeCode != null && c.CostTypeCode.StartsWith("VOL-COST"))
            .Select(c => c.CostTypeCode!)
            .ToListAsync(ct);
        var set = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var add = new List<Cost>();
        for (var i = 1; i <= TargetCount; i++)
        {
            var code = $"VOL-COST-{i:0000}";
            if (set.Contains(code) || billIds[i] == Guid.Empty)
            {
                continue;
            }

            var amount = 1_200_000m + (i * 15_000m);
            var date = today.AddDays(-(i % 80));
            var cost = new Cost
            {
                TenantId = tid,
                BillId = billIds[i],
                CostTypeCode = code,
                VendorPartyId = vendorIds[i],
                AttributionType = CostAttributionTypes.Direct,
                ExpectedAmount = amount,
                Amount = amount,
                CurrencyCode = "VND",
                BaseAmount = amount,
                SourceType = CostSourceTypes.Manual,
                SourceId = Guid.NewGuid(),
                EffectiveDate = date,
                RecordStatus = "active"
            };
            ApplyCostMaturity(cost, i, amount, now);
            add.Add(cost);
        }

        if (add.Count > 0)
        {
            _db.Costs.AddRange(add);
            await _db.SaveChangesAsync(ct);
        }
    }

    private async Task EnsureRevenuesAsync(
        Guid tid, Guid[] billIds, Guid[] customerIds, DateOnly today, DateTimeOffset now, CancellationToken ct)
    {
        var existing = await _db.Revenues.IgnoreQueryFilters()
            .Where(r => r.TenantId == tid && r.DeletedAt == null && r.RevenueTypeCode != null && r.RevenueTypeCode.StartsWith("VOL-REV"))
            .Select(r => r.RevenueTypeCode!)
            .ToListAsync(ct);
        var set = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var add = new List<Revenue>();
        for (var i = 1; i <= TargetCount; i++)
        {
            var code = $"VOL-REV-{i:0000}";
            if (set.Contains(code) || billIds[i] == Guid.Empty)
            {
                continue;
            }

            var amount = 2_400_000m + (i * 18_000m);
            var date = today.AddDays(-(i % 80));
            var rev = new Revenue
            {
                TenantId = tid,
                BillId = billIds[i],
                RevenueTypeCode = code,
                CustomerPartyId = customerIds[i],
                ExpectedAmount = amount,
                Amount = amount,
                CurrencyCode = "VND",
                BaseAmount = amount,
                SourceType = RevenueSourceTypes.Manual,
                SourceId = Guid.NewGuid(),
                EffectiveDate = date,
                RecordStatus = "active"
            };
            ApplyRevenueMaturity(rev, i, amount, now);
            add.Add(rev);
        }

        if (add.Count > 0)
        {
            _db.Revenues.AddRange(add);
            await _db.SaveChangesAsync(ct);
        }
    }

    private async Task EnsureDocumentsAsync(
        Guid tid, Guid[] billIds, Guid[] vendorIds, Guid[] customerIds, DateOnly today, DateTimeOffset now, CancellationToken ct)
    {
        var existing = await _db.FinancialDocuments.IgnoreQueryFilters()
            .Where(d => d.TenantId == tid && d.DeletedAt == null && d.DocumentNo.StartsWith("VOL-DOC-"))
            .Select(d => d.DocumentNo)
            .ToListAsync(ct);
        var set = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var docs = new List<FinancialDocument>();
        for (var i = 1; i <= TargetCount; i++)
        {
            if (billIds[i] == Guid.Empty)
            {
                continue;
            }

            var payNo = Code("VOL-DOC-AP-", i);
            var recNo = Code("VOL-DOC-AR-", i);
            if (!set.Contains(payNo))
            {
                docs.Add(MakeDoc(tid, billIds[i], vendorIds[i], payNo, FinancialDocumentDirections.Payable, today, now, i));
            }

            if (!set.Contains(recNo))
            {
                docs.Add(MakeDoc(tid, billIds[i], customerIds[i], recNo, FinancialDocumentDirections.Receivable, today, now, i));
            }
        }

        if (docs.Count == 0)
        {
            return;
        }

        _db.FinancialDocuments.AddRange(docs);
        await _db.SaveChangesAsync(ct);
        _db.FinancialDocumentLines.AddRange(docs.Select(d => new FinancialDocumentLine
        {
            TenantId = tid,
            DocumentId = d.Id,
            LineNo = 1,
            Description = d.DocumentNo,
            Amount = d.TotalAmount,
            CurrencyCode = "VND",
            BillId = d.BillId
        }));
        await _db.SaveChangesAsync(ct);
    }

    private async Task EnsureApArAsync(
        Guid tid, Guid[] billIds, Guid[] vendorIds, Guid[] customerIds, DateOnly today, DateTimeOffset now, CancellationToken ct)
    {
        var existingPay = await _db.PayableExposures.IgnoreQueryFilters()
            .Where(e => e.TenantId == tid && e.DeletedAt == null && e.Notes != null && e.Notes.StartsWith("VOL-EXP-AP-"))
            .Select(e => e.Notes!)
            .ToListAsync(ct);
        var existingRec = await _db.ReceivableExposures.IgnoreQueryFilters()
            .Where(e => e.TenantId == tid && e.DeletedAt == null && e.Notes != null && e.Notes.StartsWith("VOL-EXP-AR-"))
            .Select(e => e.Notes!)
            .ToListAsync(ct);
        var paySet = existingPay.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var recSet = existingRec.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var pays = new List<PayableExposure>();
        var recs = new List<ReceivableExposure>();
        for (var i = 1; i <= TargetCount; i++)
        {
            if (billIds[i] == Guid.Empty)
            {
                continue;
            }

            var apNote = Code("VOL-EXP-AP-", i);
            if (!paySet.Contains(apNote))
            {
                var amount = 1_500_000m + (i * 12_000m);
                pays.Add(new PayableExposure
                {
                    TenantId = tid,
                    BillId = billIds[i],
                    CounterpartyId = vendorIds[i],
                    Amount = amount,
                    RecognizedAmount = amount,
                    CurrencyCode = "VND",
                    Status = ExposureStatuses.FullyRecognized,
                    EffectiveDate = today.AddDays(-(i % 60)),
                    DueDate = today.AddDays(15 + (i % 20)),
                    Notes = apNote,
                    RecordStatus = "active"
                });
            }

            var arNote = Code("VOL-EXP-AR-", i);
            if (!recSet.Contains(arNote))
            {
                var amount = 2_200_000m + (i * 14_000m);
                recs.Add(new ReceivableExposure
                {
                    TenantId = tid,
                    BillId = billIds[i],
                    CounterpartyId = customerIds[i],
                    Amount = amount,
                    RecognizedAmount = amount,
                    CurrencyCode = "VND",
                    Status = ExposureStatuses.FullyRecognized,
                    EffectiveDate = today.AddDays(-(i % 60)),
                    DueDate = today.AddDays(20 + (i % 25)),
                    Notes = arNote,
                    RecordStatus = "active"
                });
            }
        }

        if (pays.Count > 0)
        {
            _db.PayableExposures.AddRange(pays);
        }

        if (recs.Count > 0)
        {
            _db.ReceivableExposures.AddRange(recs);
        }

        if (pays.Count + recs.Count > 0)
        {
            await _db.SaveChangesAsync(ct);
        }

        var allPayExps = await _db.PayableExposures.IgnoreQueryFilters()
            .Where(e => e.TenantId == tid && e.DeletedAt == null && e.Notes != null && e.Notes.StartsWith("VOL-EXP-AP-"))
            .ToListAsync(ct);
        var allRecExps = await _db.ReceivableExposures.IgnoreQueryFilters()
            .Where(e => e.TenantId == tid && e.DeletedAt == null && e.Notes != null && e.Notes.StartsWith("VOL-EXP-AR-"))
            .ToListAsync(ct);

        var existingAp = await _db.AccountsPayable.IgnoreQueryFilters()
            .Where(a => a.TenantId == tid && a.DeletedAt == null && a.Notes != null && a.Notes.StartsWith("VOL-AP-"))
            .Select(a => a.Notes!)
            .ToListAsync(ct);
        var existingAr = await _db.AccountsReceivable.IgnoreQueryFilters()
            .Where(a => a.TenantId == tid && a.DeletedAt == null && a.Notes != null && a.Notes.StartsWith("VOL-AR-"))
            .Select(a => a.Notes!)
            .ToListAsync(ct);
        var apSet = existingAp.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var arSet = existingAr.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var apRows = new List<AccountsPayable>();
        foreach (var exp in allPayExps)
        {
            var note = exp.Notes!.Replace("VOL-EXP-AP-", "VOL-AP-", StringComparison.Ordinal);
            if (apSet.Contains(note))
            {
                continue;
            }

            var settled = decimal.Round(exp.Amount * (apRows.Count % 3 == 0 ? 0m : apRows.Count % 3 == 1 ? 0.4m : 1m), 0);
            apRows.Add(new AccountsPayable
            {
                TenantId = tid,
                PayableExposureId = exp.Id,
                BillId = exp.BillId,
                CounterpartyId = exp.CounterpartyId,
                RecognizedAmount = exp.Amount,
                FinalizedSettledAmount = settled,
                CurrencyCode = "VND",
                DueDate = exp.DueDate,
                SettlementStatus = settled == 0
                    ? ApArSettlementStatuses.Open
                    : settled >= exp.Amount
                        ? ApArSettlementStatuses.Settled
                        : ApArSettlementStatuses.PartiallySettled,
                RecognizedAt = now.AddDays(-1),
                RecordStatus = "active",
                Notes = note
            });
        }

        var arRows = new List<AccountsReceivable>();
        foreach (var exp in allRecExps)
        {
            var note = exp.Notes!.Replace("VOL-EXP-AR-", "VOL-AR-", StringComparison.Ordinal);
            if (arSet.Contains(note))
            {
                continue;
            }

            var settled = decimal.Round(exp.Amount * (arRows.Count % 3 == 0 ? 0m : arRows.Count % 3 == 1 ? 0.35m : 1m), 0);
            arRows.Add(new AccountsReceivable
            {
                TenantId = tid,
                ReceivableExposureId = exp.Id,
                BillId = exp.BillId,
                CounterpartyId = exp.CounterpartyId,
                RecognizedAmount = exp.Amount,
                FinalizedSettledAmount = settled,
                CurrencyCode = "VND",
                DueDate = exp.DueDate,
                SettlementStatus = settled == 0
                    ? ApArSettlementStatuses.Open
                    : settled >= exp.Amount
                        ? ApArSettlementStatuses.Settled
                        : ApArSettlementStatuses.PartiallySettled,
                RecognizedAt = now.AddDays(-1),
                RecordStatus = "active",
                Notes = note
            });
        }

        if (apRows.Count > 0)
        {
            _db.AccountsPayable.AddRange(apRows);
        }

        if (arRows.Count > 0)
        {
            _db.AccountsReceivable.AddRange(arRows);
        }

        if (apRows.Count + arRows.Count > 0)
        {
            await _db.SaveChangesAsync(ct);
        }
    }

    private async Task EnsureSettlementsAsync(
        Guid tid, Guid[] billIds, Guid[] vendorIds, Guid[] customerIds, DateOnly today, CancellationToken ct)
    {
        var existingPay = await _db.Payments.IgnoreQueryFilters()
            .Where(p => p.TenantId == tid && p.DeletedAt == null && p.ReferenceNo != null && p.ReferenceNo.StartsWith("VOL-PAY-"))
            .Select(p => p.ReferenceNo!)
            .ToListAsync(ct);
        var existingCol = await _db.Collections.IgnoreQueryFilters()
            .Where(c => c.TenantId == tid && c.DeletedAt == null && c.ReferenceNo != null && c.ReferenceNo.StartsWith("VOL-COL-"))
            .Select(c => c.ReferenceNo!)
            .ToListAsync(ct);
        var paySet = existingPay.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var colSet = existingCol.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var pays = new List<Payment>();
        var cols = new List<Collection>();
        for (var i = 1; i <= TargetCount; i++)
        {
            if (billIds[i] == Guid.Empty)
            {
                continue;
            }

            var payNo = Code("VOL-PAY-", i);
            if (!paySet.Contains(payNo))
            {
                var amount = 400_000m + (i * 5_000m);
                pays.Add(new Payment
                {
                    TenantId = tid,
                    BillId = billIds[i],
                    CounterpartyId = vendorIds[i],
                    Amount = amount,
                    BaseAmount = amount,
                    CurrencyCode = "VND",
                    ValueDate = today.AddDays(-(i % 40)),
                    ReferenceNo = payNo,
                    Notes = "Thanh toán mẫu VOL",
                    Status = PaymentStatuses.Open,
                    RecordStatus = "active"
                });
            }

            var colNo = Code("VOL-COL-", i);
            if (!colSet.Contains(colNo))
            {
                var amount = 550_000m + (i * 6_000m);
                cols.Add(new Collection
                {
                    TenantId = tid,
                    BillId = billIds[i],
                    CounterpartyId = customerIds[i],
                    Amount = amount,
                    BaseAmount = amount,
                    CurrencyCode = "VND",
                    ValueDate = today.AddDays(-(i % 40)),
                    ReferenceNo = colNo,
                    Notes = "Thu tiền mẫu VOL",
                    Status = CollectionStatuses.Open,
                    RecordStatus = "active"
                });
            }
        }

        if (pays.Count > 0)
        {
            _db.Payments.AddRange(pays);
        }

        if (cols.Count > 0)
        {
            _db.Collections.AddRange(cols);
        }

        if (pays.Count + cols.Count > 0)
        {
            await _db.SaveChangesAsync(ct);
        }
    }

    private async Task EnsureRateCardsAsync(Guid tid, DateTimeOffset now, CancellationToken ct)
    {
        var existing = await _db.RateCards.IgnoreQueryFilters()
            .Where(r => r.TenantId == tid && r.DeletedAt == null && r.Code.StartsWith("VOL-RC-"))
            .Select(r => r.Code)
            .ToListAsync(ct);
        var set = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var cards = new List<RateCard>();
        for (var i = 1; i <= TargetCount; i++)
        {
            var code = Code("VOL-RC-", i);
            if (set.Contains(code))
            {
                continue;
            }

            cards.Add(new RateCard
            {
                TenantId = tid,
                Code = code,
                Name = i % 2 == 0 ? $"Bảng giá bán mẫu {i:0000}" : $"Bảng giá mua mẫu {i:0000}",
                PartyType = i % 2 == 0 ? "customer" : "vendor",
                CurrencyCode = "VND",
                Description = "Bảng giá dữ liệu mẫu VOL",
                IsActive = i % 19 != 0
            });
        }

        if (cards.Count == 0)
        {
            return;
        }

        _db.RateCards.AddRange(cards);
        await _db.SaveChangesAsync(ct);
        var versions = cards.Select(c => new RateVersion
        {
            TenantId = tid,
            RateCardId = c.Id,
            VersionNo = 1,
            Status = RateVersionStatuses.Published,
            EffectiveFrom = now.AddDays(-30),
            PublishedAt = now.AddDays(-30),
            Note = "Phiên bản mẫu VOL"
        }).ToList();
        _db.RateVersions.AddRange(versions);
        await _db.SaveChangesAsync(ct);
        _db.PricingRules.AddRange(versions.Select((v, idx) => new PricingRule
        {
            TenantId = tid,
            RateVersionId = v.Id,
            Code = "FREIGHT",
            Name = "Cước chính",
            CalcMethod = PricingCalcMethods.Fixed,
            UnitAmount = 1_000_000m + (idx * 8_000m),
            CurrencyCode = "VND",
            SortOrder = 10,
            IsActive = true
        }));
        await _db.SaveChangesAsync(ct);
    }

    private async Task EnsureBankFeedAsync(Guid tid, DateOnly today, CancellationToken ct)
    {
        var existing = await _db.BankFeedLines.IgnoreQueryFilters()
            .Where(b => b.TenantId == tid && b.DeletedAt == null && b.BankReference != null && b.BankReference.StartsWith("VOL-BANK-"))
            .Select(b => b.BankReference!)
            .ToListAsync(ct);
        var set = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var add = new List<BankFeedLine>();
        for (var i = 1; i <= TargetCount; i++)
        {
            var code = Code("VOL-BANK-", i);
            if (set.Contains(code))
            {
                continue;
            }

            add.Add(new BankFeedLine
            {
                TenantId = tid,
                ValueDate = today.AddDays(-(i % 45)),
                Amount = 800_000m + (i * 7_000m),
                CurrencyCode = "VND",
                Direction = i % 2 == 0 ? BankFeedDirections.Credit : BankFeedDirections.Debit,
                BankReference = code,
                CounterpartyName = i % 2 == 0 ? $"Khách hàng mẫu {i:0000}" : $"NCC mẫu {i:0000}",
                Description = "Dòng sao kê mẫu VOL",
                Status = BankFeedLineStatuses.Unmatched
            });
        }

        if (add.Count > 0)
        {
            _db.BankFeedLines.AddRange(add);
            await _db.SaveChangesAsync(ct);
        }
    }

    private static void ApplyCostMaturity(Cost cost, int i, decimal amount, DateTimeOffset now)
    {
        switch (i % 3)
        {
            case 1:
                cost.FinancialMaturity = CostMaturities.Confirmed;
                cost.ConfirmedAmount = amount;
                cost.ConfirmedAt = now.AddDays(-2);
                cost.Amount = amount;
                break;
            case 2:
                cost.FinancialMaturity = CostMaturities.Actual;
                cost.ConfirmedAmount = amount;
                cost.ActualAmount = amount + 25_000m;
                cost.ConfirmedAt = now.AddDays(-4);
                cost.ActualizedAt = now.AddDays(-1);
                cost.Amount = cost.ActualAmount.Value;
                cost.BaseAmount = cost.ActualAmount;
                break;
            default:
                cost.FinancialMaturity = CostMaturities.Expected;
                break;
        }
    }

    private static void ApplyRevenueMaturity(Revenue rev, int i, decimal amount, DateTimeOffset now)
    {
        switch (i % 3)
        {
            case 1:
                rev.FinancialMaturity = RevenueMaturities.Confirmed;
                rev.ConfirmedAmount = amount;
                rev.ConfirmedAt = now.AddDays(-2);
                rev.Amount = amount;
                break;
            case 2:
                rev.FinancialMaturity = RevenueMaturities.Actual;
                rev.ConfirmedAmount = amount;
                rev.ActualAmount = amount + 40_000m;
                rev.ConfirmedAt = now.AddDays(-4);
                rev.ActualizedAt = now.AddDays(-1);
                rev.Amount = rev.ActualAmount.Value;
                rev.BaseAmount = rev.ActualAmount;
                break;
            default:
                rev.FinancialMaturity = RevenueMaturities.Expected;
                break;
        }
    }

    private static FinancialDocument MakeDoc(
        Guid tid, Guid billId, Guid partyId, string no, string direction, DateOnly today, DateTimeOffset now, int i)
    {
        var amount = (direction == FinancialDocumentDirections.Payable ? 1_100_000m : 2_000_000m) + (i * 9_000m);
        var receipt = i % 9 == 0
            ? FinancialDocumentReceiptStatuses.NotReceived
            : FinancialDocumentReceiptStatuses.Received;
        var acceptance = receipt == FinancialDocumentReceiptStatuses.NotReceived
            ? FinancialDocumentAcceptanceStatuses.NotAccepted
            : i % 7 == 0
                ? FinancialDocumentAcceptanceStatuses.Rejected
                : i % 4 == 0
                    ? FinancialDocumentAcceptanceStatuses.NotAccepted
                    : FinancialDocumentAcceptanceStatuses.Accepted;
        return new FinancialDocument
        {
            TenantId = tid,
            BillId = billId,
            CounterpartyId = partyId,
            DocumentNo = no,
            DocumentType = FinancialDocumentTypes.Invoice,
            Direction = direction,
            TotalAmount = amount,
            CurrencyCode = "VND",
            DocumentDate = today.AddDays(-(i % 50)),
            ReceiptStatus = receipt,
            AcceptanceStatus = acceptance,
            MatchingStatus = FinancialDocumentMatchingStatuses.Unmatched,
            ReceivedAt = receipt == FinancialDocumentReceiptStatuses.Received ? now.AddDays(-1) : null,
            AcceptedAt = acceptance == FinancialDocumentAcceptanceStatuses.Accepted ? now : null,
            SourceSystem = SourceSystem,
            ExternalId = no,
            Notes = $"Chứng từ mẫu {no}"
        };
    }

    private static OperationalContextDocument SampleContext(
        int i, (string Origin, string Dest, string Route) lane, string mode, bool customer) => new()
    {
        ServiceType = mode == "air" ? "AIR" : "FREIGHT",
        Incoterm = i % 2 == 0 ? "FOB" : "CIF",
        PickupLocation = lane.Origin,
        DeliveryLocation = lane.Dest,
        PackageCount = 2 + (i % 8),
        GrossWeightKg = 120 + (i % 40),
        VolumeCbm = 1.2m + (i % 5) * 0.1m,
        ChargeableWeightKg = 130 + (i % 40),
        Commodity = "Hàng tổng hợp mẫu",
        CargoDescription = $"Hàng mẫu {lane.Route} lô {i:0000}",
        ShipperName = customer ? $"Người gửi {i:0000}" : null,
        ConsigneeName = customer ? $"Người nhận {i:0000}" : null,
        PreferredCurrency = "VND",
        ExtraServices = i % 4 == 0 ? ["pickup", "delivery"] : ["customs"]
    };

    private async Task<int> CountPartiesAsync(Guid tid, string prefix, CancellationToken ct) =>
        await _db.BusinessParties.IgnoreQueryFilters()
            .CountAsync(p => p.TenantId == tid && p.DeletedAt == null && p.Code.StartsWith(prefix), ct);

    private async Task<int> CountCatalogAsync(Guid tid, string kind, string prefix, CancellationToken ct) =>
        await _db.MasterCatalogItems.IgnoreQueryFilters()
            .CountAsync(i => i.TenantId == tid && i.DeletedAt == null && i.Kind == kind && i.Code.StartsWith(prefix), ct);

    private async Task<HashSet<string>> CatalogCodesAsync(Guid tid, string kind, string prefix, CancellationToken ct)
    {
        var rows = await _db.MasterCatalogItems.IgnoreQueryFilters()
            .Where(i => i.TenantId == tid && i.DeletedAt == null && i.Kind == kind && i.Code.StartsWith(prefix))
            .Select(i => i.Code)
            .ToListAsync(ct);
        return rows.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static Guid[] MapToIndex(IReadOnlyList<IdCode> rows, string prefix)
    {
        var ids = new Guid[TargetCount + 1];
        foreach (var row in rows)
        {
            if (TryIndex(row.Code, prefix, out var i))
            {
                ids[i] = row.Id;
            }
        }

        return ids;
    }

    private static string Code(string prefix, int i) => prefix + i.ToString("0000");

    private static bool TryIndex(string value, string prefix, out int index)
    {
        index = 0;
        if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return int.TryParse(value[prefix.Length..], out index) && index is >= 1 and <= TargetCount;
    }

    private readonly record struct IdCode(Guid Id, string Code);
}

public sealed record DemoVolumeResult(bool Skipped, string Summary, IReadOnlyDictionary<string, int> Counts);
