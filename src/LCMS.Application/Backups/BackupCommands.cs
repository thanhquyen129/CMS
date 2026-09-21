using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Backups;

public sealed record TenantBackupListItemDto(
    Guid Id,
    string Kind,
    string Status,
    int ByteSize,
    string ChecksumSha256,
    string? Note,
    DateTimeOffset CreatedAt,
    DateTimeOffset? RestoredAt);

public sealed record TenantBackupDetailDto(
    Guid Id,
    string Kind,
    string Status,
    string PayloadJson,
    int ByteSize,
    string ChecksumSha256,
    string? Note,
    DateTimeOffset CreatedAt,
    DateTimeOffset? RestoredAt);

public sealed record ListTenantBackupsQuery : IRequest<IReadOnlyList<TenantBackupListItemDto>>;
public sealed record GetTenantBackupQuery(Guid Id) : IRequest<TenantBackupDetailDto>;
public sealed record CreateTenantBackupCommand(string? Note) : IRequest<Guid>;
public sealed record RestoreTenantBackupCommand(Guid Id, string ConfirmPhrase) : IRequest;

public sealed class CreateTenantBackupCommandValidator : AbstractValidator<CreateTenantBackupCommand>
{
    public CreateTenantBackupCommandValidator()
    {
        RuleFor(x => x.Note).MaximumLength(512).When(x => x.Note is not null);
    }
}

public sealed class RestoreTenantBackupCommandValidator : AbstractValidator<RestoreTenantBackupCommand>
{
    public RestoreTenantBackupCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.ConfirmPhrase).NotEmpty().WithMessage("Nhập cụm xác nhận khôi phục.");
    }
}

public sealed class ListTenantBackupsQueryHandler
    : IRequestHandler<ListTenantBackupsQuery, IReadOnlyList<TenantBackupListItemDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService _permissions;

    public ListTenantBackupsQueryHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IPermissionService permissions)
    {
        _db = db;
        _tenantContext = tenantContext;
        _permissions = permissions;
    }

    public async Task<IReadOnlyList<TenantBackupListItemDto>> Handle(
        ListTenantBackupsQuery request,
        CancellationToken cancellationToken)
    {
        await EnsureAsync(_tenantContext, _permissions, cancellationToken);
        return await _db.TenantBackups.AsNoTracking()
            .OrderByDescending(b => b.CreatedAt)
            .Take(50)
            .Select(b => new TenantBackupListItemDto(
                b.Id, b.Kind, b.Status, b.ByteSize, b.ChecksumSha256, b.Note, b.CreatedAt, b.RestoredAt))
            .ToListAsync(cancellationToken);
    }

    internal static async Task EnsureAsync(
        ITenantContext tenantContext,
        IPermissionService permissions,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        await permissions.EnsureAsync(
            PermissionCodes.BackupManage,
            "Bạn không có quyền sao lưu / khôi phục danh mục.",
            cancellationToken);
    }
}

public sealed class GetTenantBackupQueryHandler : IRequestHandler<GetTenantBackupQuery, TenantBackupDetailDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService _permissions;

    public GetTenantBackupQueryHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IPermissionService permissions)
    {
        _db = db;
        _tenantContext = tenantContext;
        _permissions = permissions;
    }

    public async Task<TenantBackupDetailDto> Handle(GetTenantBackupQuery request, CancellationToken cancellationToken)
    {
        await ListTenantBackupsQueryHandler.EnsureAsync(_tenantContext, _permissions, cancellationToken);
        var row = await _db.TenantBackups.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == request.Id, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy bản sao lưu.");
        return new TenantBackupDetailDto(
            row.Id, row.Kind, row.Status, row.PayloadJson, row.ByteSize, row.ChecksumSha256,
            row.Note, row.CreatedAt, row.RestoredAt);
    }
}

public sealed class CreateTenantBackupCommandHandler : IRequestHandler<CreateTenantBackupCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService _permissions;
    private readonly IAuditWriter _audit;

    public CreateTenantBackupCommandHandler(
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

    public async Task<Guid> Handle(CreateTenantBackupCommand request, CancellationToken cancellationToken)
    {
        await ListTenantBackupsQueryHandler.EnsureAsync(_tenantContext, _permissions, cancellationToken);
        var tenantId = _tenantContext.TenantId!.Value;
        var payload = await LogicalMasterSnapshot.CaptureAsync(_db, cancellationToken);
        var json = JsonSerializer.Serialize(payload, LogicalMasterSnapshot.Json);
        var bytes = Encoding.UTF8.GetBytes(json);
        var checksum = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        var row = new TenantBackup
        {
            TenantId = tenantId,
            Kind = TenantBackupKinds.LogicalMaster,
            Status = TenantBackupStatuses.Completed,
            PayloadJson = json,
            ChecksumSha256 = checksum,
            ByteSize = bytes.Length,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim()
        };
        _db.TenantBackups.Add(row);
        _audit.Append(
            AuditActions.BackupCreate,
            AuditObjectTypes.Backup,
            row.Id,
            afterJson: JsonSerializer.Serialize(new { row.ByteSize, row.ChecksumSha256 }));
        await _db.SaveChangesAsync(cancellationToken);
        return row.Id;
    }
}

public sealed class RestoreTenantBackupCommandHandler : IRequestHandler<RestoreTenantBackupCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService _permissions;
    private readonly IAuditWriter _audit;

    public RestoreTenantBackupCommandHandler(
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

    public async Task Handle(RestoreTenantBackupCommand request, CancellationToken cancellationToken)
    {
        await ListTenantBackupsQueryHandler.EnsureAsync(_tenantContext, _permissions, cancellationToken);
        var tenant = await _db.Tenants.FirstAsync(t => t.Id == _tenantContext.TenantId, cancellationToken);
        var expected = $"RESTORE {tenant.Code}";
        if (!string.Equals(request.ConfirmPhrase.Trim(), expected, StringComparison.Ordinal))
        {
            throw new ConflictAppException($"Cụm xác nhận phải đúng: {expected}");
        }

        var row = await _db.TenantBackups.FirstOrDefaultAsync(b => b.Id == request.Id, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy bản sao lưu.");
        LogicalMasterSnapshot.Payload payload;
        try
        {
            payload = JsonSerializer.Deserialize<LogicalMasterSnapshot.Payload>(row.PayloadJson, LogicalMasterSnapshot.Json)
                ?? throw new ConflictAppException("Bản sao lưu không đọc được.");
        }
        catch (JsonException)
        {
            throw new ConflictAppException("Bản sao lưu không đọc được.");
        }

        await LogicalMasterSnapshot.RestoreAsync(_db, payload, cancellationToken);
        row.RestoredAt = DateTimeOffset.UtcNow;
        _audit.Append(
            AuditActions.BackupRestore,
            AuditObjectTypes.Backup,
            row.Id,
            reason: "Khôi phục danh mục / cấu hình — không đụng sổ tiền.");
        await _db.SaveChangesAsync(cancellationToken);
    }
}

internal static class LogicalMasterSnapshot
{
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public sealed record CatalogRow(string Kind, string Code, string Name, string? Description, bool IsActive, int SortOrder, string? AttributesJson);
    public sealed record CurrencyRow(string Code, string Name, int DecimalPlaces, bool IsActive);
    public sealed record FxRow(string From, string To, DateOnly RateDate, decimal Rate, string? Source, string? Note);
    public sealed record OrgRow(string Code, string Name, string? ParentCode, bool IsActive);
    public sealed record ProfileRow(
        string Name, string? LegalName, string? TaxId, string? Phone, string? Email, string? Website,
        string? AddressLine1, string? AddressLine2, string? Ward, string? District, string? City, string? Province,
        string? CountryCode, string? PostalCode, string TimeZoneId, string DateFormat, string DefaultCurrencyCode);
    public sealed record NotifyRow(bool InAppEnabled, bool EmailEnabled, string EventsJson);
    public sealed record ModuleRow(string ModuleCode, bool IncludedInPlan, bool IsEnabled);
    public sealed record Payload(
        int Version,
        ProfileRow Profile,
        List<CatalogRow> Catalog,
        List<CurrencyRow> Currencies,
        List<FxRow> FxRates,
        List<OrgRow> Organizations,
        NotifyRow? Notifications,
        List<ModuleRow> Modules);

    public static async Task<Payload> CaptureAsync(ILcmsDbContext db, CancellationToken ct)
    {
        var tenant = await db.Tenants.AsNoTracking().FirstAsync(ct);
        var catalog = await db.MasterCatalogItems.AsNoTracking()
            .OrderBy(i => i.Kind).ThenBy(i => i.Code)
            .Select(i => new CatalogRow(i.Kind, i.Code, i.Name, i.Description, i.IsActive, i.SortOrder, i.AttributesJson))
            .ToListAsync(ct);
        var currencies = await db.Currencies.AsNoTracking()
            .OrderBy(c => c.Code)
            .Select(c => new CurrencyRow(c.Code, c.Name, c.DecimalPlaces, c.IsActive))
            .ToListAsync(ct);
        var fx = await db.FxRates.AsNoTracking()
            .OrderBy(r => r.RateDate).ThenBy(r => r.FromCurrencyCode)
            .Select(r => new FxRow(r.FromCurrencyCode, r.ToCurrencyCode, r.RateDate, r.Rate, r.Source, r.Note))
            .ToListAsync(ct);
        var orgs = await db.Organizations.AsNoTracking().ToListAsync(ct);
        var orgById = orgs.ToDictionary(o => o.Id);
        var orgRows = orgs.OrderBy(o => o.Code).Select(o =>
        {
            string? parentCode = null;
            if (o.ParentId is Guid pid && orgById.TryGetValue(pid, out var p))
            {
                parentCode = p.Code;
            }

            return new OrgRow(o.Code, o.Name, parentCode, o.IsActive);
        }).ToList();
        var notify = await db.TenantNotificationSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        var modules = await db.TenantLicenseModules.AsNoTracking()
            .Select(m => new ModuleRow(m.ModuleCode, m.IncludedInPlan, m.IsEnabled))
            .ToListAsync(ct);

        return new Payload(
            1,
            new ProfileRow(
                tenant.Name, tenant.LegalName, tenant.TaxId, tenant.Phone, tenant.Email, tenant.Website,
                tenant.AddressLine1, tenant.AddressLine2, tenant.Ward, tenant.District, tenant.City, tenant.Province,
                tenant.CountryCode, tenant.PostalCode,
                string.IsNullOrWhiteSpace(tenant.TimeZoneId) ? TenantDefaults.TimeZoneId : tenant.TimeZoneId,
                string.IsNullOrWhiteSpace(tenant.DateFormat) ? TenantDefaults.DateFormat : tenant.DateFormat,
                string.IsNullOrWhiteSpace(tenant.DefaultCurrencyCode) ? TenantDefaults.CurrencyCode : tenant.DefaultCurrencyCode),
            catalog,
            currencies,
            fx,
            orgRows,
            notify is null ? null : new NotifyRow(notify.InAppEnabled, notify.EmailEnabled, notify.EventsJson),
            modules);
    }

    public static async Task RestoreAsync(ILcmsDbContext db, Payload payload, CancellationToken ct)
    {
        var tenant = await db.Tenants.FirstAsync(ct);
        var p = payload.Profile;
        tenant.Name = p.Name;
        tenant.LegalName = p.LegalName;
        tenant.TaxId = p.TaxId;
        tenant.Phone = p.Phone;
        tenant.Email = p.Email;
        tenant.Website = p.Website;
        tenant.AddressLine1 = p.AddressLine1;
        tenant.AddressLine2 = p.AddressLine2;
        tenant.Ward = p.Ward;
        tenant.District = p.District;
        tenant.City = p.City;
        tenant.Province = p.Province;
        tenant.CountryCode = p.CountryCode;
        tenant.PostalCode = p.PostalCode;
        tenant.TimeZoneId = p.TimeZoneId;
        tenant.DateFormat = p.DateFormat;
        tenant.DefaultCurrencyCode = p.DefaultCurrencyCode;

        foreach (var item in payload.Catalog)
        {
            var row = await db.MasterCatalogItems.FirstOrDefaultAsync(
                i => i.Kind == item.Kind && i.Code == item.Code, ct);
            if (row is null)
            {
                db.MasterCatalogItems.Add(new MasterCatalogItem
                {
                    TenantId = tenant.Id,
                    Kind = item.Kind,
                    Code = item.Code,
                    Name = item.Name,
                    Description = item.Description,
                    IsActive = item.IsActive,
                    SortOrder = item.SortOrder,
                    AttributesJson = item.AttributesJson
                });
            }
            else
            {
                row.Name = item.Name;
                row.Description = item.Description;
                row.IsActive = item.IsActive;
                row.SortOrder = item.SortOrder;
                row.AttributesJson = item.AttributesJson;
            }
        }

        foreach (var c in payload.Currencies)
        {
            var row = await db.Currencies.FirstOrDefaultAsync(x => x.Code == c.Code, ct);
            if (row is null)
            {
                db.Currencies.Add(new Currency
                {
                    Code = c.Code,
                    Name = c.Name,
                    DecimalPlaces = c.DecimalPlaces,
                    IsActive = c.IsActive
                });
            }
            else
            {
                row.Name = c.Name;
                row.DecimalPlaces = c.DecimalPlaces;
                row.IsActive = c.IsActive;
            }
        }

        if (payload.Notifications is { } n)
        {
            var row = await db.TenantNotificationSettings.FirstOrDefaultAsync(ct);
            if (row is null)
            {
                db.TenantNotificationSettings.Add(new TenantNotificationSetting
                {
                    TenantId = tenant.Id,
                    InAppEnabled = n.InAppEnabled,
                    EmailEnabled = n.EmailEnabled,
                    EventsJson = n.EventsJson
                });
            }
            else
            {
                row.InAppEnabled = n.InAppEnabled;
                row.EmailEnabled = n.EmailEnabled;
                row.EventsJson = n.EventsJson;
            }
        }

        foreach (var m in payload.Modules)
        {
            var row = await db.TenantLicenseModules.FirstOrDefaultAsync(x => x.ModuleCode == m.ModuleCode, ct);
            if (row is not null && row.IncludedInPlan)
            {
                row.IsEnabled = m.IsEnabled;
            }
        }
    }
}
