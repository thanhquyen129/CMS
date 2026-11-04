using System.Text.Json;
using LCMS.Application.Abstractions;
using LCMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Surcharges;

public sealed record OptionalServiceAttributes(
    IReadOnlyList<string> AllowedContextTypes,
    string DefaultUom);

public static class OptionalServiceAttributesParser
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static OptionalServiceAttributes Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new OptionalServiceAttributes(
                [ServiceSelectionContexts.Bill, ServiceSelectionContexts.Shipment, ServiceSelectionContexts.Leg],
                "PACKAGE");
        }

        try
        {
            var dto = JsonSerializer.Deserialize<AttributesDto>(json, Json);
            var contexts = (dto?.AllowedContextTypes ?? [])
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c.Trim().ToUpperInvariant())
                .Distinct()
                .ToList();
            if (contexts.Count == 0)
            {
                contexts = [ServiceSelectionContexts.Bill, ServiceSelectionContexts.Shipment, ServiceSelectionContexts.Leg];
            }

            var uom = string.IsNullOrWhiteSpace(dto?.DefaultUom) ? "PACKAGE" : dto!.DefaultUom.Trim().ToUpperInvariant();
            return new OptionalServiceAttributes(contexts, uom);
        }
        catch
        {
            return new OptionalServiceAttributes(
                [ServiceSelectionContexts.Bill, ServiceSelectionContexts.Shipment, ServiceSelectionContexts.Leg],
                "PACKAGE");
        }
    }

    public static string Serialize(OptionalServiceAttributes attributes) =>
        JsonSerializer.Serialize(new AttributesDto(attributes.AllowedContextTypes, attributes.DefaultUom), Json);

    private sealed record AttributesDto(IReadOnlyList<string>? AllowedContextTypes, string? DefaultUom);
}

public static class OptionalServiceCatalogSeeder
{
    public static readonly (string Code, string Name, string Description, bool IsActive, string[] AllowedContextTypes, string DefaultUom)[] Baseline =
    [
        ("DONGGOI", "Đóng gói hàng hóa", "Dịch vụ đóng gói và bao bọc hàng hóa bổ sung", true, [ServiceSelectionContexts.Bill, ServiceSelectionContexts.Shipment, ServiceSelectionContexts.Leg], "PACKAGE"),
        ("KIEMDEM", "Kiểm đếm tại kho", "Dịch vụ kiểm đếm số lượng, quy cách hàng hóa tại kho", true, [ServiceSelectionContexts.Bill, ServiceSelectionContexts.Shipment, ServiceSelectionContexts.Leg], "BILL"),
        ("NANGHA", "Nâng hạ bốc xếp", "Dịch vụ nâng hạ, bốc xếp kiện hàng nặng hoặc đặc biệt", true, [ServiceSelectionContexts.Bill, ServiceSelectionContexts.Shipment, ServiceSelectionContexts.Leg], "SHIPMENT"),
        ("BAOHIEM", "Bảo hiểm hàng hóa giá trị cao", "Dịch vụ bảo hiểm bổ sung theo giá trị khai báo", false, [ServiceSelectionContexts.Bill], "BILL")
    ];

    public static async Task EnsureForTenantAsync(
        ILcmsDbContext db,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var existing = await db.MasterCatalogItems.IgnoreQueryFilters()
            .Where(m => m.TenantId == tenantId && m.Kind == MasterCatalogKinds.OptionalService && m.DeletedAt == null)
            .ToListAsync(cancellationToken);
        var byCode = existing.ToDictionary(m => m.Code, StringComparer.OrdinalIgnoreCase);
        var changed = false;

        foreach (var (code, name, desc, active, contexts, uom) in Baseline)
        {
            var attributes = OptionalServiceAttributesParser.Serialize(new OptionalServiceAttributes(contexts, uom));
            if (byCode.TryGetValue(code, out var row))
            {
                if (row.Name != name || row.Description != desc || row.AttributesJson != attributes)
                {
                    row.Name = name;
                    row.Description = desc;
                    row.AttributesJson = attributes;
                    changed = true;
                }
                continue;
            }

            db.MasterCatalogItems.Add(new MasterCatalogItem
            {
                TenantId = tenantId,
                Kind = MasterCatalogKinds.OptionalService,
                Code = code,
                Name = name,
                Description = desc,
                IsActive = active,
                AttributesJson = attributes,
                SortOrder = 10
            });
            changed = true;
        }

        if (changed)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public static async Task EnsureForAllTenantsAsync(
        ILcmsDbContext db,
        CancellationToken cancellationToken = default)
    {
        var tenantIds = await db.Tenants.IgnoreQueryFilters()
            .Where(t => t.DeletedAt == null)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        foreach (var id in tenantIds)
        {
            await EnsureForTenantAsync(db, id, cancellationToken);
        }
    }
}

