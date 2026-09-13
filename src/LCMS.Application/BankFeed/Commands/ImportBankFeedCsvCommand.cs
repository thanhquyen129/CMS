using System.Globalization;
using System.Text;
using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.BankFeed.Commands;

public sealed record ImportBankFeedCsvCommand(string Csv)
    : IRequest<ImportBankFeedCsvResult>;

public sealed record ImportBankFeedCsvResult(
    int Imported,
    int Skipped,
    IReadOnlyList<string> Errors);

public sealed class ImportBankFeedCsvCommandValidator : AbstractValidator<ImportBankFeedCsvCommand>
{
    public ImportBankFeedCsvCommandValidator()
    {
        RuleFor(x => x.Csv)
            .NotEmpty().WithMessage("Nội dung CSV không được để trống.");
    }
}

/// <summary>
/// Import bank feed lines from CSV (ADR-0013). Header required:
/// valueDate,amount,currencyCode,direction,bankReference,counterpartyName,description
/// </summary>
public sealed class ImportBankFeedCsvCommandHandler
    : IRequestHandler<ImportBankFeedCsvCommand, ImportBankFeedCsvResult>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ISender _sender;

    public ImportBankFeedCsvCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ISender sender)
    {
        _db = db;
        _tenantContext = tenantContext;
        _sender = sender;
    }

    public async Task<ImportBankFeedCsvResult> Handle(
        ImportBankFeedCsvCommand request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var lines = SplitCsvLines(request.Csv);
        if (lines.Count == 0)
        {
            throw new ConflictAppException("CSV trống.");
        }

        var header = ParseCsvRow(lines[0]).Select(h => h.Trim().ToLowerInvariant()).ToList();
        var idx = IndexMap(header);
        if (!idx.ContainsKey("valuedate") || !idx.ContainsKey("amount") || !idx.ContainsKey("currencycode"))
        {
            throw new ConflictAppException(
                "CSV cần header: valueDate,amount,currencyCode[,direction,bankReference,counterpartyName,description].");
        }

        var imported = 0;
        var skipped = 0;
        var errors = new List<string>();

        for (var i = 1; i < lines.Count; i++)
        {
            var rowNum = i + 1;
            var cols = ParseCsvRow(lines[i]);
            if (cols.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            try
            {
                var valueDateRaw = Cell(cols, idx, "valuedate");
                if (!DateOnly.TryParse(valueDateRaw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var valueDate)
                    && !DateOnly.TryParse(valueDateRaw, new CultureInfo("vi-VN"), DateTimeStyles.None, out valueDate))
                {
                    errors.Add($"Dòng {rowNum}: valueDate không hợp lệ.");
                    skipped++;
                    continue;
                }

                var amountRaw = Cell(cols, idx, "amount").Replace(",", ".");
                if (!decimal.TryParse(amountRaw, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount)
                    || amount <= 0)
                {
                    errors.Add($"Dòng {rowNum}: amount phải > 0.");
                    skipped++;
                    continue;
                }

                var currency = Cell(cols, idx, "currencycode");
                if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
                {
                    errors.Add($"Dòng {rowNum}: currencyCode phải 3 ký tự.");
                    skipped++;
                    continue;
                }

                var direction = Cell(cols, idx, "direction");
                if (string.IsNullOrWhiteSpace(direction))
                {
                    direction = BankFeedDirections.Credit;
                }

                var bankRef = NullIfEmpty(Cell(cols, idx, "bankreference"));
                if (!string.IsNullOrWhiteSpace(bankRef))
                {
                    var exists = await _db.BankFeedLines.AsNoTracking()
                        .AnyAsync(
                            l => l.BankReference == bankRef && l.Status != BankFeedLineStatuses.Ignored,
                            cancellationToken);
                    if (exists)
                    {
                        skipped++;
                        errors.Add($"Dòng {rowNum}: bỏ qua trùng bankReference '{bankRef}'.");
                        continue;
                    }
                }

                await _sender.Send(
                    new CreateBankFeedLineCommand(
                        valueDate,
                        amount,
                        currency,
                        direction,
                        bankRef,
                        NullIfEmpty(Cell(cols, idx, "counterpartyname")),
                        NullIfEmpty(Cell(cols, idx, "description"))),
                    cancellationToken);
                imported++;
            }
            catch (Exception ex) when (ex is ConflictAppException or FluentValidation.ValidationException)
            {
                skipped++;
                errors.Add($"Dòng {rowNum}: {ex.Message}");
            }
        }

        return new ImportBankFeedCsvResult(imported, skipped, errors);
    }

    private static Dictionary<string, int> IndexMap(IReadOnlyList<string> header)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < header.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(header[i]) && !map.ContainsKey(header[i]))
            {
                map[header[i]] = i;
            }
        }

        return map;
    }

    private static string Cell(IReadOnlyList<string> cols, Dictionary<string, int> idx, string key)
    {
        if (!idx.TryGetValue(key, out var i) || i >= cols.Count)
        {
            return "";
        }

        return cols[i].Trim();
    }

    private static string? NullIfEmpty(string s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static List<string> SplitCsvLines(string csv)
    {
        return csv.Replace("\r\n", "\n").Replace('\r', '\n')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    private static List<string> ParseCsvRow(string line)
    {
        var result = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    sb.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(sb.ToString());
                sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }

        result.Add(sb.ToString());
        return result;
    }
}
