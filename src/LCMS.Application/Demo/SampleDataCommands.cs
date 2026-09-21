using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Demo;

public sealed record SampleDataStatusDto(
    Guid TenantId,
    int TargetCount,
    IReadOnlyDictionary<string, int> Counts,
    bool Complete);

public sealed record GetSampleDataStatusQuery : IRequest<SampleDataStatusDto>;

public sealed record EnsureSampleDataCommand : IRequest<DemoSeedResult>;

public sealed class GetSampleDataStatusQueryHandler : IRequestHandler<GetSampleDataStatusQuery, SampleDataStatusDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService _permissions;
    private readonly DemoVolumeCatalogSeeder _volume;

    public GetSampleDataStatusQueryHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IPermissionService permissions,
        DemoVolumeCatalogSeeder volume)
    {
        _db = db;
        _tenantContext = tenantContext;
        _permissions = permissions;
        _volume = volume;
    }

    public async Task<SampleDataStatusDto> Handle(GetSampleDataStatusQuery request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        await _permissions.EnsureAsync(
            PermissionCodes.SettingsManage,
            "Bạn không có quyền xem dữ liệu mẫu.",
            cancellationToken);

        var tid = _tenantContext.TenantId!.Value;
        var counts = await _volume.CountAsync(tid, cancellationToken);
        return new SampleDataStatusDto(
            tid,
            DemoVolumeCatalogSeeder.TargetCount,
            counts,
            counts.Values.All(v => v >= DemoVolumeCatalogSeeder.TargetCount));
    }
}

public sealed class EnsureSampleDataCommandHandler : IRequestHandler<EnsureSampleDataCommand, DemoSeedResult>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService _permissions;
    private readonly DemoDataSeeder _seeder;

    public EnsureSampleDataCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IPermissionService permissions,
        DemoDataSeeder seeder)
    {
        _db = db;
        _tenantContext = tenantContext;
        _permissions = permissions;
        _seeder = seeder;
    }

    public async Task<DemoSeedResult> Handle(EnsureSampleDataCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        await _permissions.EnsureAsync(
            PermissionCodes.SettingsManage,
            "Bạn không có quyền tạo dữ liệu mẫu.",
            cancellationToken);

        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == _tenantContext.TenantId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy thuê bao.");

        return await _seeder.EnsureForTenantAsync(tenant, cancellationToken);
    }
}
