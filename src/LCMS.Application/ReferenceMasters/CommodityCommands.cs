using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.ReferenceMasters;

public sealed record UpsertCommodityCommand(
    string Code,
    string Name,
    string? Category,
    Guid? ParentId,
    bool IsDangerousGoods,
    bool IsTemperatureControlled,
    bool IsOversize,
    bool IsOverweight,
    bool IsHighValue,
    string? SpecialHandling,
    bool IsActive) : IRequest<Guid>;

public sealed class UpsertCommodityCommandValidator : AbstractValidator<UpsertCommodityCommand>
{
    public UpsertCommodityCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Category).MaximumLength(128);
        RuleFor(x => x.SpecialHandling).MaximumLength(512);
    }
}

/// <summary>Creates or updates a commodity type and its rating flags.</summary>
public sealed class UpsertCommodityCommandHandler : IRequestHandler<UpsertCommodityCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IPermissionService _permissions;
    private readonly IAuditWriter _audit;

    public UpsertCommodityCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenant,
        IPermissionService permissions,
        IAuditWriter audit)
    {
        _db = db;
        _tenant = tenant;
        _permissions = permissions;
        _audit = audit;
    }

    public async Task<Guid> Handle(UpsertCommodityCommand request, CancellationToken cancellationToken)
    {
        if (!_tenant.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        await _permissions.EnsureAsync(PermissionCodes.MasterCatalogManage, "Bạn không có quyền quản lý danh mục.", cancellationToken);
        if (request.ParentId is Guid parentId)
        {
            var parentOk = await _db.CommodityTypes.AsNoTracking().AnyAsync(c => c.Id == parentId && c.IsActive, cancellationToken);
            if (!parentOk)
            {
                throw new ConflictAppException("Nhóm hàng cha không tồn tại hoặc đã ngừng dùng.");
            }
        }

        var code = request.Code.Trim().ToUpperInvariant();
        var row = await _db.CommodityTypes.FirstOrDefaultAsync(c => c.Code == code, cancellationToken);
        if (row is null)
        {
            row = new CommodityType { TenantId = _tenant.TenantId!.Value, Code = code };
            _db.CommodityTypes.Add(row);
        }

        if (request.ParentId == row.Id)
        {
            throw new ConflictAppException("Loại hàng không được là cha của chính nó.");
        }

        row.Name = request.Name.Trim();
        row.Category = string.IsNullOrWhiteSpace(request.Category) ? null : request.Category.Trim();
        row.ParentId = request.ParentId;
        row.IsDangerousGoods = request.IsDangerousGoods;
        row.IsTemperatureControlled = request.IsTemperatureControlled;
        row.IsOversize = request.IsOversize;
        row.IsOverweight = request.IsOverweight;
        row.IsHighValue = request.IsHighValue;
        row.SpecialHandling = string.IsNullOrWhiteSpace(request.SpecialHandling) ? null : request.SpecialHandling.Trim();
        row.IsActive = request.IsActive;
        _audit.Append(AuditActions.CommodityUpsert, AuditObjectTypes.CommodityType, row.Id, afterJson: code);
        await _db.SaveChangesAsync(cancellationToken);
        return row.Id;
    }
}

public sealed record CommodityDto(
    Guid Id,
    string Code,
    string Name,
    string? Category,
    Guid? ParentId,
    bool IsDangerousGoods,
    bool IsTemperatureControlled,
    bool IsOversize,
    bool IsOverweight,
    bool IsHighValue,
    string? SpecialHandling,
    bool IsActive);

public sealed record ListCommoditiesQuery(bool ActiveOnly) : IRequest<IReadOnlyList<CommodityDto>>;

/// <summary>Lists commodity types for admin and cargo pickers.</summary>
public sealed class ListCommoditiesQueryHandler : IRequestHandler<ListCommoditiesQuery, IReadOnlyList<CommodityDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenant;

    public ListCommoditiesQueryHandler(ILcmsDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<CommodityDto>> Handle(ListCommoditiesQuery request, CancellationToken cancellationToken)
    {
        if (!_tenant.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var query = _db.CommodityTypes.AsNoTracking();
        if (request.ActiveOnly)
        {
            query = query.Where(c => c.IsActive);
        }

        var rows = await query.OrderBy(c => c.Code).Take(500).ToListAsync(cancellationToken);
        return rows.Select(c => new CommodityDto(
            c.Id,
            c.Code,
            c.Name,
            c.Category,
            c.ParentId,
            c.IsDangerousGoods,
            c.IsTemperatureControlled,
            c.IsOversize,
            c.IsOverweight,
            c.IsHighValue,
            c.SpecialHandling,
            c.IsActive)).ToList();
    }
}
