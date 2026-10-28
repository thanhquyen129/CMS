using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using LCMS.Domain.Pricing;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Catalog;

/// <summary>
/// Tenant transport-mode gate. New writes store UPPERCASE.
/// An empty catalog does not invent rows. Historical rows are not updated here.
/// </summary>
public static class TransportModeCatalog
{
    public static string? Canonical(string? value) => RateFieldPolicy.ToStoredMode(value);

    public static async Task<string?> ResolveAsync(
        ILcmsDbContext db,
        string? value,
        bool allowMultimodal,
        CancellationToken cancellationToken)
    {
        var code = Canonical(value);
        if (code is null)
        {
            return null;
        }

        if (code.Length > 32)
        {
            throw new ValidationAppException("Phương thức vận chuyển tối đa 32 ký tự.");
        }

        if (ShipmentModePolicy.IsMultimodal(code))
        {
            if (!allowMultimodal)
            {
                throw new ValidationAppException("Đa phương thức chỉ là phân loại của Shipment, không gán cho chặng.");
            }

            return code;
        }

        var codes = await db.MasterCatalogItems.AsNoTracking()
            .Where(m => m.Kind == MasterCatalogKinds.TransportMode && m.IsActive)
            .Select(m => m.Code)
            .ToListAsync(cancellationToken);
        if (codes.Count > 0 && !codes.Any(c => RateFieldPolicy.SameMode(c, code)))
        {
            throw new ValidationAppException(RateFieldPolicy.ModeNotInCatalog);
        }

        return code;
    }
}
