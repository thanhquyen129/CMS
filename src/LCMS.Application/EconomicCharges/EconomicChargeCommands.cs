using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.EconomicCharges;

public sealed record EconomicChargeTypeDto(Guid Id, string Code, string Name, bool IsActive);

public sealed record ListEconomicChargeTypesQuery : IRequest<IReadOnlyList<EconomicChargeTypeDto>>;

public sealed class ListEconomicChargeTypesQueryHandler : IRequestHandler<ListEconomicChargeTypesQuery, IReadOnlyList<EconomicChargeTypeDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenant;

    public ListEconomicChargeTypesQueryHandler(ILcmsDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<EconomicChargeTypeDto>> Handle(ListEconomicChargeTypesQuery request, CancellationToken cancellationToken)
    {
        if (!_tenant.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        return await _db.EconomicChargeTypes.AsNoTracking()
            .OrderBy(t => t.Code)
            .Select(t => new EconomicChargeTypeDto(t.Id, t.Code, t.Name, t.IsActive))
            .ToListAsync(cancellationToken);
    }
}

public sealed record UpsertEconomicChargeTypeCommand(string Code, string Name) : IRequest<Guid>;

public sealed class UpsertEconomicChargeTypeCommandValidator : AbstractValidator<UpsertEconomicChargeTypeCommand>
{
    public UpsertEconomicChargeTypeCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
    }
}

public sealed class UpsertEconomicChargeTypeCommandHandler : IRequestHandler<UpsertEconomicChargeTypeCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IPermissionService _permissions;

    public UpsertEconomicChargeTypeCommandHandler(ILcmsDbContext db, ITenantContext tenant, IPermissionService permissions)
    {
        _db = db;
        _tenant = tenant;
        _permissions = permissions;
    }

    public async Task<Guid> Handle(UpsertEconomicChargeTypeCommand request, CancellationToken cancellationToken)
    {
        if (!_tenant.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        await EnsurePricingWriteAsync(cancellationToken);
        var code = request.Code.Trim().ToUpperInvariant();
        var existing = await _db.EconomicChargeTypes.FirstOrDefaultAsync(t => t.Code == code, cancellationToken);
        if (existing is null)
        {
            existing = new EconomicChargeType
            {
                TenantId = _tenant.TenantId!.Value,
                Code = code,
                Name = request.Name.Trim(),
                IsActive = true
            };
            _db.EconomicChargeTypes.Add(existing);
        }
        else
        {
            existing.Name = request.Name.Trim();
            existing.IsActive = true;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return existing.Id;
    }

    private async Task EnsurePricingWriteAsync(CancellationToken cancellationToken)
    {
        if (await _permissions.HasPermissionAsync(PermissionCodes.RateBuyWrite, cancellationToken)
            || await _permissions.HasPermissionAsync(PermissionCodes.RateSellWrite, cancellationToken))
        {
            return;
        }

        throw new ForbiddenAppException("Bạn không có quyền khai báo khoản mục kinh tế.");
    }
}

public sealed record MapChargeTypeCommand(string SourceKind, string SourceCode, Guid EconomicChargeTypeId) : IRequest<Guid>;

public sealed class MapChargeTypeCommandValidator : AbstractValidator<MapChargeTypeCommand>
{
    public MapChargeTypeCommandValidator()
    {
        RuleFor(x => x.SourceKind).Must(k => ChargeTypeMappingKinds.All.Contains(k ?? ""))
            .WithMessage("Nguồn mapping phải là cost_type, revenue_type hoặc component.");
        RuleFor(x => x.SourceCode).NotEmpty().MaximumLength(64);
        RuleFor(x => x.EconomicChargeTypeId).NotEmpty();
    }
}

public sealed class MapChargeTypeCommandHandler : IRequestHandler<MapChargeTypeCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IPermissionService _permissions;

    public MapChargeTypeCommandHandler(ILcmsDbContext db, ITenantContext tenant, IPermissionService permissions)
    {
        _db = db;
        _tenant = tenant;
        _permissions = permissions;
    }

    public async Task<Guid> Handle(MapChargeTypeCommand request, CancellationToken cancellationToken)
    {
        if (!_tenant.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        if (!await _permissions.HasPermissionAsync(PermissionCodes.RateBuyWrite, cancellationToken)
            && !await _permissions.HasPermissionAsync(PermissionCodes.RateSellWrite, cancellationToken))
        {
            throw new ForbiddenAppException("Bạn không có quyền gắn khoản mục kinh tế.");
        }

        var typeExists = await _db.EconomicChargeTypes.AnyAsync(t => t.Id == request.EconomicChargeTypeId && t.IsActive, cancellationToken);
        if (!typeExists)
        {
            throw new NotFoundAppException("Không tìm thấy khoản mục kinh tế.");
        }

        var kind = request.SourceKind.Trim().ToLowerInvariant();
        var code = request.SourceCode.Trim().ToUpperInvariant();
        var row = await _db.ChargeTypeMappings.FirstOrDefaultAsync(
            m => m.SourceKind == kind && m.SourceCode == code,
            cancellationToken);
        if (row is null)
        {
            row = new ChargeTypeMapping
            {
                TenantId = _tenant.TenantId!.Value,
                SourceKind = kind,
                SourceCode = code,
                EconomicChargeTypeId = request.EconomicChargeTypeId
            };
            _db.ChargeTypeMappings.Add(row);
        }
        else
        {
            row.EconomicChargeTypeId = request.EconomicChargeTypeId;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return row.Id;
    }
}
