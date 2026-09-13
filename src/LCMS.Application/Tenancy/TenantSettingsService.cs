using System.Text.Json;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.Costs;
using LCMS.Application.Settlements;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LCMS.Application.Tenancy;

public sealed record TenantSettingsDto(
    Guid TenantId,
    string? UiJson,
    string? FinancialJson,
    TenantFinancialSettings Financial);

public interface ITenantSettingsService
{
    Task<TenantSettingsDto> GetAsync(CancellationToken cancellationToken);
    Task<TenantSettingsDto> UpsertAsync(string? uiJson, string? financialJson, CancellationToken cancellationToken);
    Task<TenantFinancialSettings> GetFinancialAsync(CancellationToken cancellationToken);
}

public sealed class TenantSettingsService : ITenantSettingsService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService _permissions;

    public TenantSettingsService(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IPermissionService permissions)
    {
        _db = db;
        _tenantContext = tenantContext;
        _permissions = permissions;
    }

    public async Task<TenantSettingsDto> GetAsync(CancellationToken cancellationToken)
    {
        EnsureTenant();
        var row = await _db.TenantSettings.AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);
        return Map(row);
    }

    public async Task<TenantSettingsDto> UpsertAsync(
        string? uiJson,
        string? financialJson,
        CancellationToken cancellationToken)
    {
        EnsureTenant();
        await _permissions.EnsureAsync(
            PermissionCodes.SettingsManage,
            "Bạn không có quyền quản lý cài đặt thuê bao.",
            cancellationToken);
        var tenantId = _tenantContext.TenantId!.Value;
        var row = await _db.TenantSettings.FirstOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            row = new TenantSetting { TenantId = tenantId };
            _db.TenantSettings.Add(row);
        }

        if (uiJson is not null)
        {
            row.UiJson = string.IsNullOrWhiteSpace(uiJson) ? null : uiJson.Trim();
        }

        if (financialJson is not null)
        {
            _ = ParseFinancial(financialJson);
            row.FinancialJson = string.IsNullOrWhiteSpace(financialJson) ? null : financialJson.Trim();
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Map(row);
    }

    public async Task<TenantFinancialSettings> GetFinancialAsync(CancellationToken cancellationToken)
    {
        var dto = await GetAsync(cancellationToken);
        return dto.Financial;
    }

    private TenantSettingsDto Map(TenantSetting? row)
    {
        var tenantId = _tenantContext.TenantId!.Value;
        var financial = ParseFinancial(row?.FinancialJson);
        return new TenantSettingsDto(tenantId, row?.UiJson, row?.FinancialJson, financial);
    }

    public static TenantFinancialSettings ParseFinancial(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new TenantFinancialSettings();
        }

        try
        {
            return JsonSerializer.Deserialize<TenantFinancialSettings>(json, JsonOpts)
                   ?? new TenantFinancialSettings();
        }
        catch (JsonException)
        {
            return new TenantFinancialSettings();
        }
    }

    private void EnsureTenant()
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }
    }
}

/// <summary>Resolves write-off / recognition / confirm threshold: tenant override → node defaults (P20).</summary>
public sealed class TenantFinancialOptionsResolver
{
    private readonly ITenantSettingsService _settings;
    private readonly SettlementOptions _settlement;
    private readonly CostOptions _cost;

    public TenantFinancialOptionsResolver(
        ITenantSettingsService settings,
        IOptions<SettlementOptions> settlement,
        IOptions<CostOptions> cost)
    {
        _settings = settings;
        _settlement = settlement.Value;
        _cost = cost.Value;
    }

    public async Task<decimal> GetMaxWriteOffAmountAsync(CancellationToken ct)
    {
        var fin = await _settings.GetFinancialAsync(ct);
        return fin.MaxWriteOffAmount ?? _settlement.MaxWriteOffAmount;
    }

    public async Task<decimal?> GetConfirmApprovalThresholdBaseAsync(CancellationToken ct)
    {
        var fin = await _settings.GetFinancialAsync(ct);
        return fin.ConfirmApprovalThresholdBase ?? _cost.ConfirmApprovalThresholdBase;
    }

    public async Task<(string Mode, string Version)> GetRecognitionPolicyAsync(CancellationToken ct)
    {
        var fin = await _settings.GetFinancialAsync(ct);
        var mode = string.IsNullOrWhiteSpace(fin.RecognitionPolicyMode)
            ? RecognitionPolicyModes.Manual
            : fin.RecognitionPolicyMode.Trim().ToLowerInvariant();
        if (mode is not (RecognitionPolicyModes.Manual or RecognitionPolicyModes.RequireDocumentLink))
        {
            mode = RecognitionPolicyModes.Manual;
        }

        var version = string.IsNullOrWhiteSpace(fin.RecognitionPolicyVersion)
            ? "tenant-default-v1"
            : fin.RecognitionPolicyVersion.Trim();
        return (mode, version);
    }
}
