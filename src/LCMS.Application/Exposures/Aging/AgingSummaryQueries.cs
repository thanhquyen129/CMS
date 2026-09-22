using System.Globalization;
using System.Text;
using LCMS.Application.Abstractions;
using LCMS.Application.Audit;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.Exposures.Queries;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using MediatR;

namespace LCMS.Application.Exposures.Aging;

public sealed record AgingSummaryDto(
    DateOnly AsOf,
    bool CanViewPayable,
    bool CanViewReceivable,
    ApArAgingReportDto? Payable,
    ApArAgingReportDto? Receivable,
    string Note);

public sealed record GetAgingSummaryQuery(
    DateOnly? AsOf,
    Guid? CounterpartyId,
    string? CurrencyCode,
    bool IncludeSettled) : IRequest<AgingSummaryDto>;

public sealed class GetAgingSummaryQueryHandler : IRequestHandler<GetAgingSummaryQuery, AgingSummaryDto>
{
    private readonly ISender _sender;
    private readonly IPermissionService _permissions;
    private readonly ITenantContext _tenantContext;

    public GetAgingSummaryQueryHandler(
        ISender sender,
        IPermissionService permissions,
        ITenantContext tenantContext)
    {
        _sender = sender;
        _permissions = permissions;
        _tenantContext = tenantContext;
    }

    public async Task<AgingSummaryDto> Handle(GetAgingSummaryQuery request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var asOf = request.AsOf ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var canAp = await _permissions.HasPermissionAsync(PermissionCodes.CostRead, cancellationToken);
        var canAr = await _permissions.HasPermissionAsync(PermissionCodes.RevenueRead, cancellationToken);

        ApArAgingReportDto? payable = null;
        ApArAgingReportDto? receivable = null;

        if (canAp)
        {
            payable = await _sender.Send(
                new GetAccountsPayableAgingQuery(
                    asOf,
                    request.CounterpartyId,
                    request.CurrencyCode,
                    request.IncludeSettled),
                cancellationToken);
        }

        if (canAr)
        {
            receivable = await _sender.Send(
                new GetAccountsReceivableAgingQuery(
                    asOf,
                    request.CounterpartyId,
                    request.CurrencyCode,
                    request.IncludeSettled),
                cancellationToken);
        }

        var note = !canAp && !canAr
            ? "Không có quyền xem tuổi nợ AP/AR (cần cost.read và/hoặc revenue.read)."
            : !canAp
                ? "Đã ẩn AP — thiếu quyền xem chi phí (cost.read)."
                : !canAr
                    ? "Đã ẩn AR — thiếu quyền xem doanh thu (revenue.read)."
                    : "Tuổi nợ derived theo ADR-0006; outstanding = recognized + adjustment − finalized settled.";

        return new AgingSummaryDto(asOf, canAp, canAr, payable, receivable, note);
    }
}

public sealed record ExportAgingCsvQuery(
    string Side,
    DateOnly? AsOf,
    Guid? CounterpartyId,
    string? CurrencyCode,
    bool IncludeSettled) : IRequest<AgingCsvExportResult>;

public sealed record AgingCsvExportResult(string FileName, string CsvContent);

public sealed class ExportAgingCsvQueryHandler : IRequestHandler<ExportAgingCsvQuery, AgingCsvExportResult>
{
    private readonly ISender _sender;
    private readonly IPermissionService _permissions;
    private readonly ITenantContext _tenantContext;
    private readonly IAuditWriter _audit;

    public ExportAgingCsvQueryHandler(
        ISender sender,
        IPermissionService permissions,
        ITenantContext tenantContext,
        IAuditWriter audit)
    {
        _sender = sender;
        _permissions = permissions;
        _tenantContext = tenantContext;
        _audit = audit;
    }

    public async Task<AgingCsvExportResult> Handle(ExportAgingCsvQuery request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var side = (request.Side ?? "").Trim().ToLowerInvariant();
        var asOf = request.AsOf ?? DateOnly.FromDateTime(DateTime.UtcNow);
        AgingCsvExportResult result;

        if (side is "payable" or "ap")
        {
            await _permissions.EnsureAsync(
                PermissionCodes.CostRead,
                "Bạn không có quyền xuất tuổi nợ phải trả.",
                cancellationToken);
            var report = await _sender.Send(
                new GetAccountsPayableAgingQuery(
                    asOf, request.CounterpartyId, request.CurrencyCode, request.IncludeSettled),
                cancellationToken);
            result = new AgingCsvExportResult(
                $"aging-ap-{asOf:yyyyMMdd}.csv",
                BuildCsv("AP", report.PayableItems ?? []));
        }
        else if (side is "receivable" or "ar")
        {
            await _permissions.EnsureAsync(
                PermissionCodes.RevenueRead,
                "Bạn không có quyền xuất tuổi nợ phải thu.",
                cancellationToken);
            var report = await _sender.Send(
                new GetAccountsReceivableAgingQuery(
                    asOf, request.CounterpartyId, request.CurrencyCode, request.IncludeSettled),
                cancellationToken);
            result = new AgingCsvExportResult(
                $"aging-ar-{asOf:yyyyMMdd}.csv",
                BuildCsv("AR", report.ReceivableItems ?? []));
        }
        else
        {
            throw new ValidationAppException(new Dictionary<string, string[]>
            {
                ["Side"] = ["side phải là payable (ap) hoặc receivable (ar)."]
            });
        }

        _audit.Append(
            AuditActions.ReportExport,
            AuditObjectTypes.Report,
            Guid.Empty,
            afterJson: AuditJson.Serialize(new
            {
                report = "aging",
                side,
                asOf,
                fileName = result.FileName,
                counterpartyId = request.CounterpartyId,
                currencyCode = request.CurrencyCode,
                includeSettled = request.IncludeSettled
            }));

        return result;
    }

    private static string BuildCsv(string side, IReadOnlyList<AccountsPayableDto> apItems) =>
        BuildCsvCore(side, apItems.Select(i => (
            i.Id, i.Outstanding, i.CurrencyCode, i.DueDate, i.DaysPastDue, i.AgingBucket, i.SettlementStatus, i.BillId)));

    private static string BuildCsv(string side, IReadOnlyList<AccountsReceivableDto> arItems) =>
        BuildCsvCore(side, arItems.Select(i => (
            i.Id, i.Outstanding, i.CurrencyCode, i.DueDate, i.DaysPastDue, i.AgingBucket, i.SettlementStatus, i.BillId)));

    private static string BuildCsvCore(
        string side,
        IEnumerable<(Guid Id, decimal Outstanding, string CurrencyCode, DateOnly? DueDate, int? DaysPastDue, string AgingBucket, string SettlementStatus, Guid? BillId)> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("side,id,outstanding,currency_code,due_date,days_past_due,aging_bucket,settlement_status,bill_id");
        foreach (var r in rows)
        {
            sb.Append(CultureInfo.InvariantCulture, $"{side},");
            sb.Append(r.Id.ToString());
            sb.Append(',');
            sb.Append(r.Outstanding.ToString(CultureInfo.InvariantCulture));
            sb.Append(',');
            sb.Append(r.CurrencyCode);
            sb.Append(',');
            sb.Append(r.DueDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "");
            sb.Append(',');
            sb.Append(r.DaysPastDue?.ToString(CultureInfo.InvariantCulture) ?? "");
            sb.Append(',');
            sb.Append(r.AgingBucket);
            sb.Append(',');
            sb.Append(r.SettlementStatus);
            sb.Append(',');
            sb.Append(r.BillId?.ToString() ?? "");
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
