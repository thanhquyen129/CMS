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
    int? SortOrder,
    string? AttributesJson = null) : IRequest<Guid>;

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
        var attributes = string.IsNullOrWhiteSpace(request.AttributesJson) ? null : request.AttributesJson.Trim();
        var sort = request.SortOrder ?? 0;

        if (kind == MasterCatalogKinds.Location && attributes is not null)
        {
            var cls = TryLocationClass(attributes);
            if (cls is not null && !MasterCatalogKinds.LocationClasses.Contains(cls))
            {
                throw new ConflictAppException("Loại địa điểm phải là cảng, sân bay hoặc cửa khẩu.");
            }
        }

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
                AttributesJson = attributes,
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
        existing.AttributesJson = attributes;
        existing.IsActive = request.IsActive;
        existing.SortOrder = sort;
        await _db.SaveChangesAsync(cancellationToken);
        return existing.Id;
    }

    private static string? TryLocationClass(string attributesJson)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(attributesJson);
            if (doc.RootElement.TryGetProperty("class", out var c))
            {
                return c.GetString();
            }
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }

        return null;
    }
}
