using System.Globalization;
using System.Text;
using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Audit;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.BusinessParties.Commands;

/// <summary>One CSV/API row for business-party batch import (create-only; no merge).</summary>
public sealed record PartyImportRow(
    string Code,
    string Name,
    string? TaxId = null,
    string? LegalName = null,
    string? Phone = null,
    string? Email = null,
    string? PartyKind = null,
    string? CountryCode = null,
    string? DefaultCurrencyCode = null,
    int? PaymentTermDays = null,
    decimal? CreditLimit = null,
    string? CreditLimitCurrencyCode = null,
    string? GroupCode = null,
    string? ExternalCode = null,
    string? ShortName = null,
    string? Notes = null,
    /// <summary>Canonical codes: customer|vendor|payer|payee|… (also accept semicolon-separated string via API binder as list).</summary>
    IReadOnlyList<string>? RoleCodes = null,
    bool? IsCustomer = null,
    bool? IsVendor = null,
    bool? IsPayer = null,
    bool? IsPayee = null,
    string? AddressLine1 = null,
    string? City = null,
    string? Province = null,
    string? BankName = null,
    string? BankAccountNumber = null,
    string? BankAccountName = null,
    string? BankCurrencyCode = null,
    string? ContactName = null,
    string? ContactPhone = null,
    string? ContactEmail = null,
    string? ContactFunction = null);

public sealed record PartyImportIssue(int Row, string Field, string Message);

public sealed record PartyImportPreview(bool CanCommit, IReadOnlyList<PartyImportIssue> Issues);

public sealed record ImportBusinessPartiesRequest(IReadOnlyList<PartyImportRow> Rows);

public sealed record PreviewPartyImportCommand(IReadOnlyList<PartyImportRow> Rows)
    : IRequest<PartyImportPreview>;

public sealed record CommitPartyImportCommand(IReadOnlyList<PartyImportRow> Rows) : IRequest<int>;

public sealed class PreviewPartyImportCommandValidator : AbstractValidator<PreviewPartyImportCommand>
{
    public PreviewPartyImportCommandValidator()
    {
        RuleFor(x => x.Rows).NotNull();
    }
}

public sealed class CommitPartyImportCommandValidator : AbstractValidator<CommitPartyImportCommand>
{
    public CommitPartyImportCommandValidator()
    {
        RuleFor(x => x.Rows).NotNull();
    }
}

public sealed class PreviewPartyImportCommandHandler
    : IRequestHandler<PreviewPartyImportCommand, PartyImportPreview>
{
    private readonly PartyImportBatch _batch;

    public PreviewPartyImportCommandHandler(PartyImportBatch batch) => _batch = batch;

    public async Task<PartyImportPreview> Handle(
        PreviewPartyImportCommand request,
        CancellationToken cancellationToken)
    {
        var issues = await _batch.ValidateAsync(request.Rows, cancellationToken);
        return new PartyImportPreview(issues.Count == 0, issues);
    }
}

public sealed class CommitPartyImportCommandHandler : IRequestHandler<CommitPartyImportCommand, int>
{
    private readonly PartyImportBatch _batch;

    public CommitPartyImportCommandHandler(PartyImportBatch batch) => _batch = batch;

    public Task<int> Handle(CommitPartyImportCommand request, CancellationToken cancellationToken) =>
        _batch.CommitAsync(request.Rows, cancellationToken);
}

/// <summary>Preview then all-or-nothing create of business parties. Duplicate code/taxId/external → fail (no merge).</summary>
public sealed class PartyImportBatch
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IPermissionService _permissions;
    private readonly IAuditWriter _audit;

    public PartyImportBatch(
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

    public async Task<IReadOnlyList<PartyImportIssue>> ValidateAsync(
        IReadOnlyList<PartyImportRow> rows,
        CancellationToken cancellationToken)
    {
        if (!_tenant.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        await _permissions.EnsureAsync(
            PermissionCodes.MasterPartyManage,
            "Bạn không có quyền quản lý đối tác.",
            cancellationToken);

        var issues = new List<PartyImportIssue>();
        if (rows is null || rows.Count == 0)
        {
            issues.Add(new PartyImportIssue(0, "rows", "File không có dòng đối tác."));
            return issues;
        }

        var tenantId = _tenant.TenantId!.Value;
        var existingCodes = await _db.BusinessParties.IgnoreQueryFilters()
            .Where(p => p.TenantId == tenantId && p.DeletedAt == null)
            .Select(p => p.Code)
            .ToListAsync(cancellationToken);
        var ownedCodes = new HashSet<string>(existingCodes, StringComparer.OrdinalIgnoreCase);

        var existingTaxIds = await _db.BusinessParties
            .Where(p => p.TenantId == tenantId && p.TaxId != null)
            .Select(p => p.TaxId!)
            .ToListAsync(cancellationToken);
        var ownedTax = new HashSet<string>(existingTaxIds, StringComparer.OrdinalIgnoreCase);

        var existingExt = await _db.BusinessParties.IgnoreQueryFilters()
            .Where(p => p.TenantId == tenantId && p.DeletedAt == null && p.ExternalCode != null)
            .Select(p => p.ExternalCode!)
            .ToListAsync(cancellationToken);
        var ownedExt = new HashSet<string>(existingExt, StringComparer.OrdinalIgnoreCase);

        var activeCurrencies = await _db.Currencies.AsNoTracking()
            .Where(c => c.IsActive)
            .Select(c => c.Code)
            .ToListAsync(cancellationToken);
        var currencySet = new HashSet<string>(activeCurrencies, StringComparer.OrdinalIgnoreCase);

        var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenTax = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenExt = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < rows.Count; i++)
        {
            var rowNo = i + 1;
            var row = rows[i];
            var code = BusinessPartyFieldRules.Normalize(row.Code);
            var name = BusinessPartyFieldRules.Normalize(row.Name);

            if (code is null)
            {
                issues.Add(new PartyImportIssue(rowNo, "code", "Thiếu mã đối tác."));
            }
            else if (code.Length > 64)
            {
                issues.Add(new PartyImportIssue(rowNo, "code", "Mã đối tác không được vượt quá 64 ký tự."));
            }
            else if (!seenCodes.Add(code) || ownedCodes.Contains(code))
            {
                issues.Add(new PartyImportIssue(rowNo, "code", "Mã đối tác bị trùng (trong tệp hoặc đã tồn tại)."));
            }

            if (name is null)
            {
                issues.Add(new PartyImportIssue(rowNo, "name", "Thiếu tên đối tác."));
            }
            else if (name.Length > 256)
            {
                issues.Add(new PartyImportIssue(rowNo, "name", "Tên đối tác không được vượt quá 256 ký tự."));
            }

            var taxId = BusinessPartyFieldRules.NormalizeTaxId(row.TaxId);
            if (taxId is not null)
            {
                if (taxId.Length > 32)
                {
                    issues.Add(new PartyImportIssue(rowNo, "taxId", "Mã số thuế không được vượt quá 32 ký tự."));
                }
                else if (!seenTax.Add(taxId) || ownedTax.Contains(taxId))
                {
                    issues.Add(new PartyImportIssue(rowNo, "taxId", "Mã số thuế bị trùng (trong tệp hoặc đã tồn tại). Không gộp hồ sơ."));
                }

                var country = BusinessPartyFieldRules.NormalizeCountry(row.CountryCode);
                if (string.Equals(country, "VN", StringComparison.OrdinalIgnoreCase)
                    && !BusinessPartyFieldRules.IsValidVnTaxId(taxId))
                {
                    issues.Add(new PartyImportIssue(rowNo, "taxId", "MST Việt Nam phải gồm 10 hoặc 13 chữ số."));
                }
            }

            var ext = BusinessPartyFieldRules.Normalize(row.ExternalCode);
            if (ext is not null)
            {
                if (ext.Length > 64)
                {
                    issues.Add(new PartyImportIssue(rowNo, "externalCode", "Mã đối chiếu ngoài không được vượt quá 64 ký tự."));
                }
                else if (!seenExt.Add(ext) || ownedExt.Contains(ext))
                {
                    issues.Add(new PartyImportIssue(rowNo, "externalCode", "Mã đối chiếu ngoài bị trùng (trong tệp hoặc đã tồn tại)."));
                }
            }

            if (!string.IsNullOrWhiteSpace(row.PartyKind) && CanonicalPartyKind(row.PartyKind) is null)
            {
                issues.Add(new PartyImportIssue(rowNo, "partyKind", "Loại đối tác phải là Tổ chức hoặc Cá nhân."));
            }

            if (row.AddressLine1 is { Length: > 256 })
            {
                issues.Add(new PartyImportIssue(rowNo, "addressLine1", "Địa chỉ không được vượt quá 256 ký tự."));
            }

            if (row.City is { Length: > 128 })
            {
                issues.Add(new PartyImportIssue(rowNo, "city", "Thành phố không được vượt quá 128 ký tự."));
            }

            if (row.Province is { Length: > 128 })
            {
                issues.Add(new PartyImportIssue(rowNo, "province", "Tỉnh không được vượt quá 128 ký tự."));
            }

            ValidateBank(rowNo, row, currencySet, issues);
            ValidateContact(rowNo, row, issues);

            var countryCode = BusinessPartyFieldRules.NormalizeCountry(row.CountryCode);
            if (countryCode is not null
                && (countryCode.Length != 2 || !countryCode.All(char.IsLetter)))
            {
                issues.Add(new PartyImportIssue(rowNo, "countryCode", "Mã quốc gia phải là 2 chữ cái ISO 3166-1."));
            }

            ValidateCurrency(rowNo, "defaultCurrencyCode", row.DefaultCurrencyCode, currencySet, issues);
            ValidateCurrency(rowNo, "creditLimitCurrencyCode", row.CreditLimitCurrencyCode, currencySet, issues);

            if (row.PaymentTermDays is int days && (days < 0 || days > 3650))
            {
                issues.Add(new PartyImportIssue(rowNo, "paymentTermDays", "Điều khoản thanh toán phải từ 0 đến 3650 ngày."));
            }

            if (row.CreditLimit is decimal limit && limit < 0)
            {
                issues.Add(new PartyImportIssue(rowNo, "creditLimit", "Hạn mức công nợ không được âm."));
            }

            if (!string.IsNullOrWhiteSpace(row.Email) && !IsSimpleEmail(row.Email))
            {
                issues.Add(new PartyImportIssue(rowNo, "email", "Email không hợp lệ."));
            }

            foreach (var role in ResolveRoleCodes(row))
            {
                if (!PartyRoleCodes.IsKnown(role))
                {
                    issues.Add(new PartyImportIssue(
                        rowNo,
                        "roleCodes",
                        $"Vai trò '{role}' không hợp lệ. Dùng Khách hàng, Nhà cung cấp, Bên trả tiền, Bên nhận tiền."));
                }
            }
        }

        return issues;
    }

    public async Task<int> CommitAsync(
        IReadOnlyList<PartyImportRow> rows,
        CancellationToken cancellationToken)
    {
        var issues = await ValidateAsync(rows, cancellationToken);
        if (issues.Count > 0)
        {
            throw new ConflictAppException("Tệp nhập có lỗi. Không ghi dữ liệu.");
        }

        var tenantId = _tenant.TenantId!.Value;
        foreach (var row in rows)
        {
            var code = BusinessPartyFieldRules.Normalize(row.Code)!;
            var roleCodes = ResolveRoleCodes(row)
                .Where(PartyRoleCodes.IsKnown)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(r => r.Trim().ToLowerInvariant())
                .ToList();

            var cmd = new CreateBusinessPartyCommand(
                Code: code,
                Name: row.Name.Trim(),
                LegalName: row.LegalName,
                TaxId: row.TaxId,
                Phone: row.Phone,
                Email: row.Email,
                DefaultCurrencyCode: row.DefaultCurrencyCode,
                PaymentTermDays: row.PaymentTermDays,
                CreditLimit: row.CreditLimit,
                CreditLimitCurrencyCode: row.CreditLimitCurrencyCode,
                Notes: row.Notes,
                RoleCodes: roleCodes,
                PartyKind: CanonicalPartyKind(row.PartyKind),
                ShortName: row.ShortName,
                GroupCode: row.GroupCode,
                ExternalCode: row.ExternalCode,
                CountryCode: row.CountryCode,
                AddressLine1: row.AddressLine1,
                City: row.City,
                Province: row.Province);

            var party = new BusinessParty
            {
                TenantId = tenantId,
                Code = code,
                Name = cmd.Name,
                IsActive = true
            };
            BusinessPartyFieldRules.ApplyFields(party, cmd);
            _db.BusinessParties.Add(party);

            foreach (var roleCode in roleCodes)
            {
                _db.PartyRoles.Add(new PartyRole
                {
                    TenantId = tenantId,
                    PartyId = party.Id,
                    RoleCode = roleCode,
                    IsActive = true
                });
            }

            AddBank(tenantId, party.Id, row);
            AddContact(tenantId, party.Id, row);

            _audit.Append(
                AuditActions.BusinessPartyCreate,
                AuditObjectTypes.BusinessParty,
                party.Id,
                afterJson: AuditJson.Serialize(new
                {
                    party.Id,
                    party.Code,
                    party.Name,
                    party.TaxId,
                    party.PartyKind,
                    roles = roleCodes,
                    bankAccount = BusinessPartyFieldRules.Normalize(row.BankAccountNumber),
                    contact = BusinessPartyFieldRules.Normalize(row.ContactName),
                    source = "party_import"
                }));
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new ConflictAppException("Mã đối tác hoặc mã số thuế đã tồn tại trong thuê bao này.");
        }

        return rows.Count;
    }

    internal static IReadOnlyList<string> ResolveRoleCodes(PartyImportRow row)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (row.RoleCodes is { Count: > 0 })
        {
            foreach (var raw in row.RoleCodes)
            {
                if (string.IsNullOrWhiteSpace(raw))
                {
                    continue;
                }

                foreach (var part in raw.Split([';', '|', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (part.Length == 0)
                    {
                        continue;
                    }

                    set.Add(CanonicalRole(part) ?? part.Trim().ToLowerInvariant());
                }
            }
        }

        if (row.IsCustomer == true) set.Add(PartyRoleCodes.Customer);
        if (row.IsVendor == true) set.Add(PartyRoleCodes.Vendor);
        if (row.IsPayer == true) set.Add(PartyRoleCodes.Payer);
        if (row.IsPayee == true) set.Add(PartyRoleCodes.Payee);

        return set.ToList();
    }

    private static void ValidateBank(
        int rowNo,
        PartyImportRow row,
        HashSet<string> currencySet,
        List<PartyImportIssue> issues)
    {
        var bankName = BusinessPartyFieldRules.Normalize(row.BankName);
        var account = BusinessPartyFieldRules.Normalize(row.BankAccountNumber);
        var accountName = BusinessPartyFieldRules.Normalize(row.BankAccountName);
        var hasAny = bankName is not null || account is not null || accountName is not null
            || BusinessPartyFieldRules.Normalize(row.BankCurrencyCode) is not null;
        if (!hasAny)
        {
            return;
        }

        if (bankName is null)
        {
            issues.Add(new PartyImportIssue(rowNo, "bankName", "Thiếu tên ngân hàng."));
        }
        else if (bankName.Length > 256)
        {
            issues.Add(new PartyImportIssue(rowNo, "bankName", "Tên ngân hàng không được vượt quá 256 ký tự."));
        }

        if (account is null)
        {
            issues.Add(new PartyImportIssue(rowNo, "bankAccountNumber", "Thiếu số tài khoản."));
        }
        else if (account.Length > 64)
        {
            issues.Add(new PartyImportIssue(rowNo, "bankAccountNumber", "Số tài khoản không được vượt quá 64 ký tự."));
        }

        if (accountName is { Length: > 256 })
        {
            issues.Add(new PartyImportIssue(rowNo, "bankAccountName", "Tên tài khoản không được vượt quá 256 ký tự."));
        }

        var currency = BusinessPartyFieldRules.NormalizeCurrency(row.BankCurrencyCode) ?? "VND";
        ValidateCurrency(rowNo, "bankCurrencyCode", currency, currencySet, issues);
    }

    private static void ValidateContact(int rowNo, PartyImportRow row, List<PartyImportIssue> issues)
    {
        var name = BusinessPartyFieldRules.Normalize(row.ContactName);
        var phone = BusinessPartyFieldRules.Normalize(row.ContactPhone);
        var email = BusinessPartyFieldRules.Normalize(row.ContactEmail);
        var function = BusinessPartyFieldRules.Normalize(row.ContactFunction);
        var hasAny = name is not null || phone is not null || email is not null || function is not null;
        if (!hasAny)
        {
            return;
        }

        if (name is null)
        {
            issues.Add(new PartyImportIssue(rowNo, "contactName", "Thiếu tên người liên hệ."));
        }
        else if (name.Length > 256)
        {
            issues.Add(new PartyImportIssue(rowNo, "contactName", "Tên người liên hệ không được vượt quá 256 ký tự."));
        }

        if (phone is { Length: > 64 })
        {
            issues.Add(new PartyImportIssue(rowNo, "contactPhone", "Điện thoại liên hệ không được vượt quá 64 ký tự."));
        }

        if (email is not null && !IsSimpleEmail(email))
        {
            issues.Add(new PartyImportIssue(rowNo, "contactEmail", "Email liên hệ không hợp lệ."));
        }

        if (function is not null && CanonicalContactFunction(function) is null)
        {
            issues.Add(new PartyImportIssue(rowNo, "contactFunction", "Chức năng liên hệ phải là Kế toán, Điều vận, Pháp lý hoặc Chung."));
        }
    }

    private void AddBank(Guid tenantId, Guid partyId, PartyImportRow row)
    {
        var bankName = BusinessPartyFieldRules.Normalize(row.BankName);
        var account = BusinessPartyFieldRules.Normalize(row.BankAccountNumber);
        if (bankName is null || account is null)
        {
            return;
        }

        _db.PartyBankAccounts.Add(new PartyBankAccount
        {
            TenantId = tenantId,
            PartyId = partyId,
            BankName = bankName,
            AccountNumber = account,
            AccountName = BusinessPartyFieldRules.Normalize(row.BankAccountName),
            CurrencyCode = BusinessPartyFieldRules.NormalizeCurrency(row.BankCurrencyCode) ?? "VND",
            IsDefault = true,
            IsActive = true
        });
    }

    private void AddContact(Guid tenantId, Guid partyId, PartyImportRow row)
    {
        var name = BusinessPartyFieldRules.Normalize(row.ContactName);
        if (name is null)
        {
            return;
        }

        _db.PartyContacts.Add(new PartyContact
        {
            TenantId = tenantId,
            PartyId = partyId,
            FullName = name,
            Phone = BusinessPartyFieldRules.Normalize(row.ContactPhone),
            Email = BusinessPartyFieldRules.Normalize(row.ContactEmail),
            FunctionCode = CanonicalContactFunction(row.ContactFunction) ?? PartyContactFunctions.General,
            IsPrimary = true,
            IsActive = true
        });
    }

    private static string? CanonicalPartyKind(string? raw)
    {
        var key = Fold(raw);
        return key switch
        {
            "organization" or "to chuc" or "org" or "cong ty" => PartyKinds.Organization,
            "individual" or "ca nhan" or "nguoi" => PartyKinds.Individual,
            _ => null
        };
    }

    private static string? CanonicalRole(string raw)
    {
        return Fold(raw) switch
        {
            "customer" or "khach hang" or "kh" => PartyRoleCodes.Customer,
            "vendor" or "nha cung cap" or "ncc" => PartyRoleCodes.Vendor,
            "payer" or "ben tra tien" => PartyRoleCodes.Payer,
            "payee" or "ben nhan tien" => PartyRoleCodes.Payee,
            "bill to" or "bill_to" or "ben nhan hoa don" => PartyRoleCodes.BillTo,
            "shipper" or "nguoi gui hang" => PartyRoleCodes.Shipper,
            "consignee" or "nguoi nhan hang" => PartyRoleCodes.Consignee,
            "carrier" or "hang van chuyen" => PartyRoleCodes.Carrier,
            "agent" or "dai ly" => PartyRoleCodes.Agent,
            _ => null
        };
    }

    private static string? CanonicalContactFunction(string? raw)
    {
        return Fold(raw) switch
        {
            "general" or "chung" => PartyContactFunctions.General,
            "billing" or "ke toan" or "thu chi" => PartyContactFunctions.Billing,
            "ops" or "dieu van" => PartyContactFunctions.Ops,
            "legal" or "phap ly" => PartyContactFunctions.Legal,
            _ => null
        };
    }

    private static string? Fold(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var lowered = raw.Trim().ToLowerInvariant().Replace('đ', 'd');
        var formD = lowered.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(formD.Length);
        foreach (var c in formD)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private static void ValidateCurrency(
        int rowNo,
        string field,
        string? raw,
        HashSet<string> currencySet,
        List<PartyImportIssue> issues)
    {
        var code = BusinessPartyFieldRules.NormalizeCurrency(raw);
        if (code is null)
        {
            return;
        }

        if (code.Length != 3 || !code.All(char.IsLetter))
        {
            issues.Add(new PartyImportIssue(rowNo, field, "Mã tiền tệ phải đúng 3 chữ cái ISO 4217."));
            return;
        }

        if (!currencySet.Contains(code))
        {
            issues.Add(new PartyImportIssue(rowNo, field, $"Tiền tệ {code} không tồn tại hoặc đã ngừng dùng."));
        }
    }

    private static bool IsSimpleEmail(string email)
    {
        var t = email.Trim();
        var at = t.IndexOf('@');
        return at > 0 && at < t.Length - 1 && t.IndexOf('@', at + 1) < 0;
    }
}
