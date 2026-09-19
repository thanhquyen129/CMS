using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Catalog.Commands;

public sealed record UpsertMasterCatalogItemCommand(
    string Kind,
    string Code,
    string Name,
    string? Description,
    bool IsActive,
    int? SortOrder) : IRequest<Guid>;

public sealed class UpsertMasterCatalogItemCommandValidator : AbstractValidator<UpsertMasterCatalogItemCommand>
{
    public UpsertMasterCatalogItemCommandValidator()
    {
        RuleFor(x => x.Kind)
            .NotEmpty().WithMessage("Loại danh mục không được để trống.")
            .Must(k => MasterCatalogKinds.All.Contains(k.Trim()))
            .WithMessage("Loại danh mục không hợp lệ.");
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Mã không được để trống.")
            .MaximumLength(64).WithMessage("Mã không được vượt quá 64 ký tự.");
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên không được để trống.")
            .MaximumLength(256).WithMessage("Tên không được vượt quá 256 ký tự.");
        RuleFor(x => x.Description)
            .MaximumLength(512)
            .When(x => x.Description is not null);
    }
}

/// <summary>Idempotent upsert by (tenant, kind, code) — Manual Entry for D02 taxonomy.</summary>
public sealed class UpsertMasterCatalogItemCommandHandler
    : IRequestHandler<UpsertMasterCatalogItemCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService _permissions;

    public UpsertMasterCatalogItemCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IPermissionService permissions)
    {
        _db = db;
        _tenantContext = tenantContext;
        _permissions = permissions;
    }

    public async Task<Guid> Handle(
        UpsertMasterCatalogItemCommand request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        await _permissions.EnsureAsync(
            PermissionCodes.MasterCatalogManage,
            "Bạn không có quyền quản lý danh mục loại.",
            cancellationToken);

        var tenantId = _tenantContext.TenantId!.Value;
        var kind = request.Kind.Trim().ToLowerInvariant();
        var code = request.Code.Trim().ToUpperInvariant();
        var name = request.Name.Trim();
        var description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        var sort = request.SortOrder ?? 0;

        var existing = await _db.MasterCatalogItems.FirstOrDefaultAsync(
            i => i.Kind == kind && i.Code == code,
            cancellationToken);

        if (existing is null)
        {
            var row = new MasterCatalogItem
            {
                TenantId = tenantId,
                Kind = kind,
                Code = code,
                Name = name,
                Description = description,
                IsActive = request.IsActive,
                SortOrder = sort
            };
            _db.MasterCatalogItems.Add(row);
            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                existing = await _db.MasterCatalogItems.FirstOrDefaultAsync(
                    i => i.Kind == kind && i.Code == code,
                    cancellationToken);
                if (existing is null)
                {
                    throw new ConflictAppException("Mã danh mục đã tồn tại trong thuê bao.");
                }
            }

            if (existing is null)
            {
                return row.Id;
            }
        }

        existing.Name = name;
        existing.Description = description;
        existing.IsActive = request.IsActive;
        existing.SortOrder = sort;
        await _db.SaveChangesAsync(cancellationToken);
        return existing.Id;
    }
}
