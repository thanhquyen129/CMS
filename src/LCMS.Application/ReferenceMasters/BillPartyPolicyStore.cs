using System.Text.Json;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.ReferenceMasters;

/// <summary>Which party roles a Bill must carry, and whether a walk-in snapshot is allowed.</summary>
public sealed class BillPartyPolicy
{
    public IReadOnlyList<string> RequiredRoles { get; init; } = [];
    public bool AllowWalkIn { get; init; }
}

/// <summary>Reads and writes the tenant Bill party-role policy.</summary>
public interface IBillPartyPolicyStore
{
    Task<BillPartyPolicy> GetAsync(CancellationToken cancellationToken);
    Task SaveAsync(BillPartyPolicy policy, CancellationToken cancellationToken);
}

/// <summary>Persists Bill party policy on tenant_settings without touching other setting JSON.</summary>
public sealed class BillPartyPolicyStore : IBillPartyPolicyStore
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenant;

    public BillPartyPolicyStore(ILcmsDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<BillPartyPolicy> GetAsync(CancellationToken cancellationToken)
    {
        var row = await _db.TenantSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        return Parse(row?.BillPartyPolicyJson);
    }

    public async Task SaveAsync(BillPartyPolicy policy, CancellationToken cancellationToken)
    {
        if (!_tenant.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var row = await _db.TenantSettings.FirstOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            row = new TenantSetting { TenantId = _tenant.TenantId!.Value };
            _db.TenantSettings.Add(row);
        }

        row.BillPartyPolicyJson = JsonSerializer.Serialize(new PolicyBody(policy.RequiredRoles, policy.AllowWalkIn), Json);
    }

    internal static BillPartyPolicy Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new BillPartyPolicy();
        }

        try
        {
            var body = JsonSerializer.Deserialize<PolicyBody>(json, Json);
            var roles = (body?.RequiredRoles ?? [])
                .Select(r => r.Trim().ToLowerInvariant())
                .Where(PartyRoleCodes.IsKnown)
                .Distinct()
                .ToList();
            return new BillPartyPolicy { RequiredRoles = roles, AllowWalkIn = body?.AllowWalkIn ?? false };
        }
        catch (JsonException)
        {
            return new BillPartyPolicy();
        }
    }

    private sealed record PolicyBody(IReadOnlyList<string> RequiredRoles, bool AllowWalkIn);
}
