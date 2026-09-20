using System.Globalization;
using System.Text;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.BusinessParties.Queries;

public sealed record ExportBusinessPartyDirectoryQuery(
    string? Search = null,
    string? RoleCode = null,
    string? Status = null,
    string? Kind = null,
    string? GroupCode = null) : IRequest<byte[]>;

public sealed class ExportBusinessPartyDirectoryQueryHandler
    : IRequestHandler<ExportBusinessPartyDirectoryQuery, byte[]>
{
    public const int MaxRows = 5000;

    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService _permissions;

    public ExportBusinessPartyDirectoryQueryHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IPermissionService permissions)
    {
        _db = db;
        _tenantContext = tenantContext;
        _permissions = permissions;
    }

    public async Task<byte[]> Handle(
        ExportBusinessPartyDirectoryQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var query = PartySearch.ApplyDirectory(
            _db.BusinessParties.AsNoTracking(),
            _db,
            request.Search,
            request.RoleCode,
            request.Status,
            request.Kind,
            request.GroupCode);

        var parties = await query
            .OrderBy(p => p.Code)
            .Take(MaxRows)
            .ToListAsync(cancellationToken);

        var ids = parties.Select(p => p.Id).ToList();
        var roles = ids.Count == 0
            ? []
            : await _db.PartyRoles.AsNoTracking()
                .Where(r => ids.Contains(r.PartyId) && r.IsActive)
                .Select(r => new { r.PartyId, r.RoleCode })
                .ToListAsync(cancellationToken);
        var rolesByParty = roles
            .GroupBy(r => r.PartyId)
            .ToDictionary(g => g.Key, g => string.Join("|", g.Select(x => x.RoleCode).OrderBy(c => c)));

        var canCost = await _permissions.HasPermissionAsync(PermissionCodes.CostRead, cancellationToken);
        var canRevenue = await _permissions.HasPermissionAsync(PermissionCodes.RevenueRead, cancellationToken);

        Dictionary<Guid, decimal> apByParty = [];
        Dictionary<Guid, decimal> arByParty = [];
        if (ids.Count > 0 && canCost)
        {
            var apRows = await _db.AccountsPayable.AsNoTracking()
                .Where(a => a.CounterpartyId != null && ids.Contains(a.CounterpartyId.Value) && a.RecordStatus == ApArRecordStatuses.Active)
                .Select(a => new { a.CounterpartyId, a.RecognizedAmount, a.AdjustmentAmount, a.FinalizedSettledAmount })
                .ToListAsync(cancellationToken);
            apByParty = apRows
                .GroupBy(a => a.CounterpartyId!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => decimal.Round(
                        g.Sum(x => x.RecognizedAmount + x.AdjustmentAmount - x.FinalizedSettledAmount),
                        4,
                        MidpointRounding.AwayFromZero));
        }

        if (ids.Count > 0 && canRevenue)
        {
            var arRows = await _db.AccountsReceivable.AsNoTracking()
                .Where(a => a.CounterpartyId != null && ids.Contains(a.CounterpartyId.Value) && a.RecordStatus == ApArRecordStatuses.Active)
                .Select(a => new { a.CounterpartyId, a.RecognizedAmount, a.AdjustmentAmount, a.FinalizedSettledAmount })
                .ToListAsync(cancellationToken);
            arByParty = arRows
                .GroupBy(a => a.CounterpartyId!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => decimal.Round(
                        g.Sum(x => x.RecognizedAmount + x.AdjustmentAmount - x.FinalizedSettledAmount),
                        4,
                        MidpointRounding.AwayFromZero));
        }

        var sb = new StringBuilder();
        sb.AppendLine(
            "ma,ten,ten_viet_tat,mst,sdt,email,loai,nhom,vai_tro,trang_thai,han_muc,tien_te_han_muc,che_do_han_muc,phai_tra,phai_thu,ngay_tao");

        foreach (var p in parties)
        {
            var status = PartyStatusCodes.FromFlags(p.IsActive, p.IsBlocked);
            var ap = canCost && apByParty.TryGetValue(p.Id, out var apVal) ? apVal.ToString("0.####", CultureInfo.InvariantCulture) : "";
            var ar = canRevenue && arByParty.TryGetValue(p.Id, out var arVal) ? arVal.ToString("0.####", CultureInfo.InvariantCulture) : "";
            sb.AppendLine(string.Join(",",
                Csv(p.Code),
                Csv(p.Name),
                Csv(p.ShortName),
                Csv(p.TaxId),
                Csv(p.Phone),
                Csv(p.Email),
                Csv(p.PartyKind),
                Csv(p.GroupCode),
                Csv(rolesByParty.GetValueOrDefault(p.Id)),
                Csv(status),
                Csv(p.CreditLimit?.ToString("0.####", CultureInfo.InvariantCulture)),
                Csv(p.CreditLimitCurrencyCode),
                Csv(p.CreditControlMode),
                Csv(ap),
                Csv(ar),
                Csv(p.CreatedAt.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture))));
        }

        var preamble = Encoding.UTF8.GetPreamble();
        var body = Encoding.UTF8.GetBytes(sb.ToString());
        var bytes = new byte[preamble.Length + body.Length];
        Buffer.BlockCopy(preamble, 0, bytes, 0, preamble.Length);
        Buffer.BlockCopy(body, 0, bytes, preamble.Length, body.Length);
        return bytes;
    }

    private static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}
