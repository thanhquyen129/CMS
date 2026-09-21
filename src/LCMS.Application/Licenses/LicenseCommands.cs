using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace LCMS.Application.Licenses;

public sealed record LicenseModuleDto(string Code, string Name, bool IncludedInPlan, bool IsEnabled);

public sealed record TenantLicenseDto(
    Guid Id,
    string PlanCode,
    string PlanName,
    int SeatLimit,
    int SeatsUsed,
    DateTimeOffset ValidFrom,
    DateTimeOffset ValidUntil,
    string Status,
    string? Notes,
    IReadOnlyList<LicenseModuleDto> Modules);

public sealed record GetTenantLicenseQuery : IRequest<TenantLicenseDto>;

public sealed class GetTenantLicenseQueryHandler : IRequestHandler<GetTenantLicenseQuery, TenantLicenseDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public GetTenantLicenseQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<TenantLicenseDto> Handle(GetTenantLicenseQuery request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        await TenantLicenseSeeder.EnsureForCurrentTenantAsync(_db, _tenantContext.TenantId!.Value, cancellationToken);
        return await MapAsync(_db, cancellationToken);
    }

    internal static async Task<TenantLicenseDto> MapAsync(ILcmsDbContext db, CancellationToken cancellationToken)
    {
        var licenses = await db.TenantLicenses.AsNoTracking().ToListAsync(cancellationToken);
        var license = licenses.OrderByDescending(l => l.ValidUntil).First();
        var modules = await db.TenantLicenseModules.AsNoTracking()
            .Where(m => m.LicenseId == license.Id)
            .ToListAsync(cancellationToken);
        var seatsUsed = await db.Users.CountAsync(u => u.IsActive, cancellationToken);
        var byCode = modules.ToDictionary(m => m.ModuleCode, StringComparer.OrdinalIgnoreCase);
        var moduleDtos = TenantModules.Catalog.Select(c =>
        {
            byCode.TryGetValue(c.Code, out var row);
            return new LicenseModuleDto(
                c.Code,
                c.NameVi,
                row?.IncludedInPlan ?? true,
                row?.IsEnabled ?? true);
        }).ToList();

        var status = license.Status;
        if (status == TenantLicenseStatuses.Active && license.ValidUntil < DateTimeOffset.UtcNow)
        {
            status = TenantLicenseStatuses.Expired;
        }

        return new TenantLicenseDto(
            license.Id,
            license.PlanCode,
            license.PlanName,
            license.SeatLimit,
            seatsUsed,
            license.ValidFrom,
            license.ValidUntil,
            status,
            license.Notes,
            moduleDtos);
    }
}

public sealed record SetLicenseModuleEnabledCommand(string ModuleCode, bool IsEnabled) : IRequest<TenantLicenseDto>;

public sealed class SetLicenseModuleEnabledCommandHandler
    : IRequestHandler<SetLicenseModuleEnabledCommand, TenantLicenseDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService _permissions;
    private readonly IAuditWriter _audit;

    public SetLicenseModuleEnabledCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IPermissionService permissions,
        IAuditWriter audit)
    {
        _db = db;
        _tenantContext = tenantContext;
        _permissions = permissions;
        _audit = audit;
    }

    public async Task<TenantLicenseDto> Handle(
        SetLicenseModuleEnabledCommand request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        await _permissions.EnsureAsync(
            PermissionCodes.LicenseManage,
            "Bạn không có quyền quản lý license.",
            cancellationToken);

        var code = request.ModuleCode.Trim().ToLowerInvariant();
        if (!TenantModules.AllCodes.Contains(code))
        {
            throw new NotFoundAppException("Module không thuộc catalog sản phẩm.");
        }

        await TenantLicenseSeeder.EnsureForCurrentTenantAsync(_db, _tenantContext.TenantId!.Value, cancellationToken);
        var licenses = await _db.TenantLicenses.ToListAsync(cancellationToken);
        var license = licenses.OrderByDescending(l => l.ValidUntil).First();
        var row = await _db.TenantLicenseModules.FirstAsync(
            m => m.LicenseId == license.Id && m.ModuleCode == code, cancellationToken);

        if (request.IsEnabled && !row.IncludedInPlan)
        {
            throw new ConflictAppException("Không thể bật module không nằm trong gói license.");
        }

        var before = JsonSerializer.Serialize(new { row.ModuleCode, row.IsEnabled });
        row.IsEnabled = request.IsEnabled;
        _audit.Append(
            AuditActions.LicenseModuleUpdate,
            AuditObjectTypes.License,
            license.Id,
            beforeJson: before,
            afterJson: JsonSerializer.Serialize(new { row.ModuleCode, row.IsEnabled }));
        await _db.SaveChangesAsync(cancellationToken);
        return await GetTenantLicenseQueryHandler.MapAsync(_db, cancellationToken);
    }
}

public static class TenantLicenseSeeder
{
    public static async Task EnsureForCurrentTenantAsync(
        ILcmsDbContext db,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var existing = await db.TenantLicenses.FirstOrDefaultAsync(
            l => l.TenantId == tenantId, cancellationToken);
        if (existing is null)
        {
            existing = new TenantLicense
            {
                TenantId = tenantId,
                PlanCode = TenantLicensePlans.Professional,
                PlanName = "Professional",
                SeatLimit = 50,
                ValidFrom = DateTimeOffset.UtcNow,
                ValidUntil = DateTimeOffset.UtcNow.AddYears(2),
                Status = TenantLicenseStatuses.Active,
                Notes = "Gói mặc định self-host — ADR-0021."
            };
            db.TenantLicenses.Add(existing);
            await db.SaveChangesAsync(cancellationToken);
        }

        var present = await db.TenantLicenseModules
            .Where(m => m.TenantId == tenantId && m.LicenseId == existing.Id)
            .Select(m => m.ModuleCode)
            .ToListAsync(cancellationToken);
        var set = present.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var (code, _) in TenantModules.Catalog)
        {
            if (set.Contains(code))
            {
                continue;
            }

            db.TenantLicenseModules.Add(new TenantLicenseModule
            {
                TenantId = tenantId,
                LicenseId = existing.Id,
                ModuleCode = code,
                IncludedInPlan = true,
                IsEnabled = true
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public static async Task EnsureForAllTenantsAsync(ILcmsDbContext db, CancellationToken cancellationToken)
    {
        var ids = await db.Tenants.Select(t => t.Id).ToListAsync(cancellationToken);
        foreach (var id in ids)
        {
            await EnsureForCurrentTenantAsync(db, id, cancellationToken);
        }
    }
}
