using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Audit;
using LCMS.Application.BusinessParties;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.BusinessParties.Commands;

public sealed record CreateBusinessPartyCommand(
    string Code,
    string Name,
    string? LegalName = null,
    string? TaxId = null,
    string? Phone = null,
    string? Email = null,
    string? Website = null,
    string? AddressLine1 = null,
    string? AddressLine2 = null,
    string? Ward = null,
    string? District = null,
    string? City = null,
    string? Province = null,
    string? CountryCode = null,
    string? PostalCode = null,
    string? DefaultCurrencyCode = null,
    int? PaymentTermDays = null,
    decimal? CreditLimit = null,
    string? CreditLimitCurrencyCode = null,
    string? Notes = null,
    IReadOnlyList<string>? RoleCodes = null,
    string? PartyKind = null,
    string? ShortName = null,
    string? LegalType = null,
    string? GroupCode = null,
    string? ExternalCode = null,
    string? IndustryCode = null,
    string? InvoiceEmail = null,
    bool? VatRegistered = null,
    Guid? AssignedUserId = null,
    Guid? ParentPartyId = null,
    string? CreditControlMode = null) : IRequest<Guid>;

public sealed class CreateBusinessPartyCommandValidator : AbstractValidator<CreateBusinessPartyCommand>
{
    public CreateBusinessPartyCommandValidator()
    {
        RuleFor(x => x.Code)
            .MaximumLength(64).WithMessage("Mã đối tác không được vượt quá 64 ký tự.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên đối tác không được để trống.")
            .MaximumLength(256).WithMessage("Tên đối tác không được vượt quá 256 ký tự.");

        BusinessPartyFieldRules.Apply(this);
    }
}

public sealed class CreateBusinessPartyCommandHandler : IRequestHandler<CreateBusinessPartyCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService _permissions;
    private readonly IAuditWriter _audit;

    public CreateBusinessPartyCommandHandler(
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

    public async Task<Guid> Handle(CreateBusinessPartyCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        await _permissions.EnsureAsync(
            PermissionCodes.MasterPartyManage,
            "Bạn không có quyền quản lý đối tác.",
            cancellationToken);

        var tenantId = _tenantContext.TenantId!.Value;
        var code = await BusinessPartyFieldRules.AllocateCodeAsync(_db, tenantId, request.Code, cancellationToken);
        var taxId = BusinessPartyFieldRules.NormalizeTaxId(request.TaxId);

        if (await _db.BusinessParties.IgnoreQueryFilters()
                .AnyAsync(p => p.TenantId == tenantId && p.Code == code, cancellationToken))
        {
            throw new ConflictAppException("Mã đối tác đã tồn tại trong thuê bao này.");
        }

        if (taxId is not null
            && await _db.BusinessParties.AnyAsync(
                p => p.TenantId == tenantId && p.TaxId == taxId,
                cancellationToken))
        {
            throw new ConflictAppException("Mã số thuế đã tồn tại trên đối tác khác trong thuê bao này.");
        }

        await BusinessPartyFieldRules.EnsureCurrencyExistsAsync(
            _db,
            request.DefaultCurrencyCode,
            request.CreditLimitCurrencyCode,
            cancellationToken);

        await BusinessPartyFieldRules.EnsureRelationsAsync(
            _db,
            tenantId,
            request.AssignedUserId,
            request.ParentPartyId,
            excludePartyId: null,
            request.ExternalCode,
            cancellationToken);

        var roleCodes = BusinessPartyFieldRules.NormalizeRoleCodes(request.RoleCodes);

        var party = new BusinessParty
        {
            TenantId = tenantId,
            Code = code,
            Name = request.Name.Trim(),
            IsActive = true
        };
        BusinessPartyFieldRules.ApplyFields(party, request);

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
                roles = roleCodes
            }));

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new ConflictAppException("Mã đối tác hoặc mã số thuế đã tồn tại trong thuê bao này.");
        }

        return party.Id;
    }
}

public sealed record UpdateBusinessPartyCommand(
    Guid Id,
    string Name,
    bool IsActive,
    string? LegalName = null,
    string? TaxId = null,
    string? Phone = null,
    string? Email = null,
    string? Website = null,
    string? AddressLine1 = null,
    string? AddressLine2 = null,
    string? Ward = null,
    string? District = null,
    string? City = null,
    string? Province = null,
    string? CountryCode = null,
    string? PostalCode = null,
    string? DefaultCurrencyCode = null,
    int? PaymentTermDays = null,
    decimal? CreditLimit = null,
    string? CreditLimitCurrencyCode = null,
    string? Notes = null,
    string? PartyKind = null,
    string? ShortName = null,
    string? LegalType = null,
    string? GroupCode = null,
    string? ExternalCode = null,
    string? IndustryCode = null,
    string? InvoiceEmail = null,
    bool? VatRegistered = null,
    Guid? AssignedUserId = null,
    Guid? ParentPartyId = null,
    string? CreditControlMode = null) : IRequest;

public sealed class UpdateBusinessPartyCommandValidator : AbstractValidator<UpdateBusinessPartyCommand>
{
    public UpdateBusinessPartyCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Đối tác không hợp lệ.");
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên đối tác không được để trống.")
            .MaximumLength(256).WithMessage("Tên đối tác không được vượt quá 256 ký tự.");

        BusinessPartyFieldRules.Apply(this);
    }
}

public sealed class UpdateBusinessPartyCommandHandler : IRequestHandler<UpdateBusinessPartyCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService _permissions;
    private readonly IAuditWriter _audit;

    public UpdateBusinessPartyCommandHandler(
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

    public async Task Handle(UpdateBusinessPartyCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        await _permissions.EnsureAsync(
            PermissionCodes.MasterPartyManage,
            "Bạn không có quyền quản lý đối tác.",
            cancellationToken);

        var tenantId = _tenantContext.TenantId!.Value;
        var party = await _db.BusinessParties.FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);
        if (party is null)
        {
            throw new NotFoundAppException("Không tìm thấy đối tác.");
        }

        var taxId = BusinessPartyFieldRules.NormalizeTaxId(request.TaxId);
        if (taxId is not null
            && await _db.BusinessParties.AnyAsync(
                p => p.TenantId == tenantId && p.TaxId == taxId && p.Id != party.Id,
                cancellationToken))
        {
            throw new ConflictAppException("Mã số thuế đã tồn tại trên đối tác khác trong thuê bao này.");
        }

        await BusinessPartyFieldRules.EnsureCurrencyExistsAsync(
            _db,
            request.DefaultCurrencyCode,
            request.CreditLimitCurrencyCode,
            cancellationToken);

        await BusinessPartyFieldRules.EnsureRelationsAsync(
            _db,
            tenantId,
            request.AssignedUserId,
            request.ParentPartyId,
            party.Id,
            request.ExternalCode,
            cancellationToken);

        var before = AuditJson.Serialize(new
        {
            party.Name,
            party.TaxId,
            party.IsActive,
            party.CreditLimit,
            party.PaymentTermDays,
            party.CreditControlMode
        });

        party.Name = request.Name.Trim();
        party.IsActive = request.IsActive;
        BusinessPartyFieldRules.ApplyFields(party, request);

        _audit.Append(
            AuditActions.BusinessPartyUpdate,
            AuditObjectTypes.BusinessParty,
            party.Id,
            beforeJson: before,
            afterJson: AuditJson.Serialize(new
            {
                party.Name,
                party.TaxId,
                party.IsActive,
                party.CreditLimit,
                party.PaymentTermDays,
                party.CreditControlMode
            }));

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new ConflictAppException("Mã số thuế đã tồn tại trên đối tác khác trong thuê bao này.");
        }
    }
}

public sealed record SoftDeleteBusinessPartyCommand(Guid Id) : IRequest;

public sealed class SoftDeleteBusinessPartyCommandHandler : IRequestHandler<SoftDeleteBusinessPartyCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService _permissions;
    private readonly ICurrentUserContext _userContext;
    private readonly IAuditWriter _audit;
    private readonly IPartyDirectoryService _directory;

    public SoftDeleteBusinessPartyCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IPermissionService permissions,
        ICurrentUserContext userContext,
        IAuditWriter audit,
        IPartyDirectoryService directory)
    {
        _db = db;
        _tenantContext = tenantContext;
        _permissions = permissions;
        _userContext = userContext;
        _audit = audit;
        _directory = directory;
    }

    public async Task Handle(SoftDeleteBusinessPartyCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        await _permissions.EnsureAsync(
            PermissionCodes.MasterPartyManage,
            "Bạn không có quyền quản lý đối tác.",
            cancellationToken);

        var party = await _db.BusinessParties.FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);
        if (party is null)
        {
            throw new NotFoundAppException("Không tìm thấy đối tác.");
        }

        var (apOpen, arOpen, apCount, arCount) = await _directory.GetOpenExposureTotalsAsync(party.Id, cancellationToken);
        if (apCount > 0 || arCount > 0)
        {
            throw new ConflictAppException(
                $"Không xóa mềm đối tác {party.Code} khi còn công nợ mở " +
                $"(AP {apCount} dòng / {apOpen:0.##}, AR {arCount} dòng / {arOpen:0.##}). " +
                "Hãy ngừng dùng hoặc chặn giao dịch.");
        }

        var actorId = _userContext.UserId;
        _audit.Append(
            AuditActions.BusinessPartyDelete,
            AuditObjectTypes.BusinessParty,
            party.Id,
            beforeJson: AuditJson.Serialize(new { party.Code, party.Name, party.IsActive }),
            reason: "soft-delete");
        party.SoftDelete(actorId);

        var roles = await _db.PartyRoles.Where(r => r.PartyId == party.Id).ToListAsync(cancellationToken);
        foreach (var role in roles)
        {
            role.SoftDelete(actorId);
        }

        var banks = await _db.PartyBankAccounts.Where(b => b.PartyId == party.Id).ToListAsync(cancellationToken);
        foreach (var bank in banks)
        {
            bank.SoftDelete(actorId);
        }

        var contacts = await _db.PartyContacts.Where(c => c.PartyId == party.Id).ToListAsync(cancellationToken);
        foreach (var contact in contacts)
        {
            contact.SoftDelete(actorId);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}

internal static class BusinessPartyFieldRules
{
    public static void Apply(AbstractValidator<CreateBusinessPartyCommand> validator)
    {
        validator.RuleFor(x => x.LegalName).MaximumLength(256)
            .WithMessage("Tên pháp lý không được vượt quá 256 ký tự.");
        validator.RuleFor(x => x.TaxId).MaximumLength(32)
            .WithMessage("Mã số thuế không được vượt quá 32 ký tự.");
        validator.RuleFor(x => x.Phone).MaximumLength(64)
            .WithMessage("Số điện thoại không được vượt quá 64 ký tự.");
        validator.RuleFor(x => x.Email).MaximumLength(256)
            .WithMessage("Email không được vượt quá 256 ký tự.");
        validator.RuleFor(x => x.Website).MaximumLength(256)
            .WithMessage("Website không được vượt quá 256 ký tự.");
        validator.RuleFor(x => x.AddressLine1).MaximumLength(256);
        validator.RuleFor(x => x.AddressLine2).MaximumLength(256);
        validator.RuleFor(x => x.Ward).MaximumLength(128);
        validator.RuleFor(x => x.District).MaximumLength(128);
        validator.RuleFor(x => x.City).MaximumLength(128);
        validator.RuleFor(x => x.Province).MaximumLength(128);
        validator.RuleFor(x => x.CountryCode)
            .MaximumLength(2)
            .Matches(@"^([A-Za-z]{2})?$")
            .WithMessage("Mã quốc gia phải là 2 chữ cái ISO 3166-1.");
        validator.RuleFor(x => x.PostalCode).MaximumLength(32);
        validator.RuleFor(x => x.DefaultCurrencyCode)
            .Must(c => string.IsNullOrWhiteSpace(c) || (c.Trim().Length == 3 && c.Trim().All(char.IsLetter)))
            .WithMessage("Mã tiền tệ mặc định phải đúng 3 chữ cái ISO 4217.");
        validator.RuleFor(x => x.PaymentTermDays)
            .InclusiveBetween(0, 3650)
            .When(x => x.PaymentTermDays.HasValue)
            .WithMessage("Điều khoản thanh toán phải từ 0 đến 3650 ngày.");
        validator.RuleFor(x => x.CreditLimit)
            .GreaterThanOrEqualTo(0)
            .When(x => x.CreditLimit.HasValue)
            .WithMessage("Hạn mức công nợ không được âm.");
        validator.RuleFor(x => x.CreditLimitCurrencyCode)
            .Must(c => string.IsNullOrWhiteSpace(c) || (c.Trim().Length == 3 && c.Trim().All(char.IsLetter)))
            .WithMessage("Mã tiền tệ hạn mức phải đúng 3 chữ cái ISO 4217.");
        validator.RuleFor(x => x.Notes).MaximumLength(2000)
            .WithMessage("Ghi chú không được vượt quá 2000 ký tự.");
        validator.RuleForEach(x => x.RoleCodes!)
            .Must(PartyRoleCodes.IsKnown)
            .When(x => x.RoleCodes is { Count: > 0 })
            .WithMessage("Mã vai trò đối tác phải là customer, vendor, payer hoặc payee.");
        validator.RuleFor(x => x.PartyKind)
            .Must(k => string.IsNullOrWhiteSpace(k) || PartyKinds.IsKnown(k))
            .WithMessage("Loại đối tác phải là tổ chức hoặc cá nhân.");
        validator.RuleFor(x => x.ShortName).MaximumLength(128);
        validator.RuleFor(x => x.LegalType)
            .Must(PartyLegalTypes.IsKnown)
            .WithMessage("Loại pháp lý không hợp lệ.");
        validator.RuleFor(x => x.GroupCode).MaximumLength(64);
        validator.RuleFor(x => x.ExternalCode).MaximumLength(64);
        validator.RuleFor(x => x.IndustryCode).MaximumLength(64);
        validator.RuleFor(x => x.InvoiceEmail).MaximumLength(256);
        validator.RuleFor(x => x.CreditControlMode)
            .Must(m => string.IsNullOrWhiteSpace(m) || PartyCreditControlModes.IsKnown(m))
            .WithMessage("Chế độ hạn mức phải là advisory, warn hoặc block.");
        validator.RuleFor(x => x.Email)
            .EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.Email))
            .WithMessage("Email không hợp lệ.");
        validator.RuleFor(x => x.InvoiceEmail)
            .EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.InvoiceEmail))
            .WithMessage("Email hóa đơn không hợp lệ.");
        validator.RuleFor(x => x.TaxId)
            .Must(t => BusinessPartyFieldRules.IsValidVnTaxId(t))
            .When(x => !string.IsNullOrWhiteSpace(x.TaxId)
                       && string.Equals(x.CountryCode?.Trim(), "VN", StringComparison.OrdinalIgnoreCase))
            .WithMessage("MST Việt Nam phải gồm 10 hoặc 13 chữ số.");
    }

    public static void Apply(AbstractValidator<UpdateBusinessPartyCommand> validator)
    {
        validator.RuleFor(x => x.LegalName).MaximumLength(256)
            .WithMessage("Tên pháp lý không được vượt quá 256 ký tự.");
        validator.RuleFor(x => x.TaxId).MaximumLength(32)
            .WithMessage("Mã số thuế không được vượt quá 32 ký tự.");
        validator.RuleFor(x => x.Phone).MaximumLength(64)
            .WithMessage("Số điện thoại không được vượt quá 64 ký tự.");
        validator.RuleFor(x => x.Email).MaximumLength(256)
            .WithMessage("Email không được vượt quá 256 ký tự.");
        validator.RuleFor(x => x.Website).MaximumLength(256)
            .WithMessage("Website không được vượt quá 256 ký tự.");
        validator.RuleFor(x => x.AddressLine1).MaximumLength(256);
        validator.RuleFor(x => x.AddressLine2).MaximumLength(256);
        validator.RuleFor(x => x.Ward).MaximumLength(128);
        validator.RuleFor(x => x.District).MaximumLength(128);
        validator.RuleFor(x => x.City).MaximumLength(128);
        validator.RuleFor(x => x.Province).MaximumLength(128);
        validator.RuleFor(x => x.CountryCode)
            .MaximumLength(2)
            .Matches(@"^([A-Za-z]{2})?$")
            .WithMessage("Mã quốc gia phải là 2 chữ cái ISO 3166-1.");
        validator.RuleFor(x => x.PostalCode).MaximumLength(32);
        validator.RuleFor(x => x.DefaultCurrencyCode)
            .Must(c => string.IsNullOrWhiteSpace(c) || (c.Trim().Length == 3 && c.Trim().All(char.IsLetter)))
            .WithMessage("Mã tiền tệ mặc định phải đúng 3 chữ cái ISO 4217.");
        validator.RuleFor(x => x.PaymentTermDays)
            .InclusiveBetween(0, 3650)
            .When(x => x.PaymentTermDays.HasValue)
            .WithMessage("Điều khoản thanh toán phải từ 0 đến 3650 ngày.");
        validator.RuleFor(x => x.CreditLimit)
            .GreaterThanOrEqualTo(0)
            .When(x => x.CreditLimit.HasValue)
            .WithMessage("Hạn mức công nợ không được âm.");
        validator.RuleFor(x => x.CreditLimitCurrencyCode)
            .Must(c => string.IsNullOrWhiteSpace(c) || (c.Trim().Length == 3 && c.Trim().All(char.IsLetter)))
            .WithMessage("Mã tiền tệ hạn mức phải đúng 3 chữ cái ISO 4217.");
        validator.RuleFor(x => x.Notes).MaximumLength(2000)
            .WithMessage("Ghi chú không được vượt quá 2000 ký tự.");
        validator.RuleFor(x => x.PartyKind)
            .Must(k => string.IsNullOrWhiteSpace(k) || PartyKinds.IsKnown(k))
            .WithMessage("Loại đối tác phải là tổ chức hoặc cá nhân.");
        validator.RuleFor(x => x.ShortName).MaximumLength(128);
        validator.RuleFor(x => x.LegalType)
            .Must(PartyLegalTypes.IsKnown)
            .WithMessage("Loại pháp lý không hợp lệ.");
        validator.RuleFor(x => x.GroupCode).MaximumLength(64);
        validator.RuleFor(x => x.ExternalCode).MaximumLength(64);
        validator.RuleFor(x => x.IndustryCode).MaximumLength(64);
        validator.RuleFor(x => x.InvoiceEmail).MaximumLength(256);
        validator.RuleFor(x => x.CreditControlMode)
            .Must(m => string.IsNullOrWhiteSpace(m) || PartyCreditControlModes.IsKnown(m))
            .WithMessage("Chế độ hạn mức phải là advisory, warn hoặc block.");
        validator.RuleFor(x => x.Email)
            .EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.Email))
            .WithMessage("Email không hợp lệ.");
        validator.RuleFor(x => x.InvoiceEmail)
            .EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.InvoiceEmail))
            .WithMessage("Email hóa đơn không hợp lệ.");
        validator.RuleFor(x => x.TaxId)
            .Must(t => BusinessPartyFieldRules.IsValidVnTaxId(t))
            .When(x => !string.IsNullOrWhiteSpace(x.TaxId)
                       && string.Equals(x.CountryCode?.Trim(), "VN", StringComparison.OrdinalIgnoreCase))
            .WithMessage("MST Việt Nam phải gồm 10 hoặc 13 chữ số.");
    }

    public static string? NormalizeTaxId(string? taxId)
    {
        var t = Normalize(taxId);
        return t;
    }

    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    public static string? NormalizeCurrency(string? code)
    {
        var c = Normalize(code);
        return c?.ToUpperInvariant();
    }

    public static string? NormalizeCountry(string? code)
    {
        var c = Normalize(code);
        return c?.ToUpperInvariant();
    }

    public static IReadOnlyList<string> NormalizeRoleCodes(IReadOnlyList<string>? roleCodes)
    {
        if (roleCodes is null || roleCodes.Count == 0)
        {
            return [];
        }

        return roleCodes
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim().ToLowerInvariant())
            .Where(PartyRoleCodes.IsKnown)
            .Distinct()
            .ToList();
    }

    public static void ApplyFields(BusinessParty party, CreateBusinessPartyCommand request)
    {
        party.LegalName = Normalize(request.LegalName);
        party.TaxId = NormalizeTaxId(request.TaxId);
        party.Phone = Normalize(request.Phone);
        party.Email = Normalize(request.Email);
        party.Website = Normalize(request.Website);
        party.AddressLine1 = Normalize(request.AddressLine1);
        party.AddressLine2 = Normalize(request.AddressLine2);
        party.Ward = Normalize(request.Ward);
        party.District = Normalize(request.District);
        party.City = Normalize(request.City);
        party.Province = Normalize(request.Province);
        party.CountryCode = NormalizeCountry(request.CountryCode);
        party.PostalCode = Normalize(request.PostalCode);
        party.DefaultCurrencyCode = NormalizeCurrency(request.DefaultCurrencyCode);
        party.PaymentTermDays = request.PaymentTermDays;
        party.CreditLimit = request.CreditLimit;
        party.CreditLimitCurrencyCode = NormalizeCurrency(request.CreditLimitCurrencyCode);
        party.Notes = Normalize(request.Notes);
        party.PartyKind = string.IsNullOrWhiteSpace(request.PartyKind)
            ? PartyKinds.Organization
            : request.PartyKind.Trim().ToLowerInvariant();
        party.ShortName = Normalize(request.ShortName);
        party.LegalType = Normalize(request.LegalType)?.ToLowerInvariant();
        party.GroupCode = Normalize(request.GroupCode);
        party.ExternalCode = Normalize(request.ExternalCode);
        party.IndustryCode = Normalize(request.IndustryCode);
        party.InvoiceEmail = Normalize(request.InvoiceEmail);
        party.VatRegistered = request.VatRegistered;
        party.AssignedUserId = request.AssignedUserId;
        party.ParentPartyId = request.ParentPartyId;
        party.CreditControlMode = string.IsNullOrWhiteSpace(request.CreditControlMode)
            ? PartyCreditControlModes.Advisory
            : request.CreditControlMode.Trim().ToLowerInvariant();
    }

    public static void ApplyFields(BusinessParty party, UpdateBusinessPartyCommand request)
    {
        party.LegalName = Normalize(request.LegalName);
        party.TaxId = NormalizeTaxId(request.TaxId);
        party.Phone = Normalize(request.Phone);
        party.Email = Normalize(request.Email);
        party.Website = Normalize(request.Website);
        party.AddressLine1 = Normalize(request.AddressLine1);
        party.AddressLine2 = Normalize(request.AddressLine2);
        party.Ward = Normalize(request.Ward);
        party.District = Normalize(request.District);
        party.City = Normalize(request.City);
        party.Province = Normalize(request.Province);
        party.CountryCode = NormalizeCountry(request.CountryCode);
        party.PostalCode = Normalize(request.PostalCode);
        party.DefaultCurrencyCode = NormalizeCurrency(request.DefaultCurrencyCode);
        party.PaymentTermDays = request.PaymentTermDays;
        party.CreditLimit = request.CreditLimit;
        party.CreditLimitCurrencyCode = NormalizeCurrency(request.CreditLimitCurrencyCode);
        party.Notes = Normalize(request.Notes);
        if (!string.IsNullOrWhiteSpace(request.PartyKind))
        {
            party.PartyKind = request.PartyKind.Trim().ToLowerInvariant();
        }

        party.ShortName = Normalize(request.ShortName);
        party.LegalType = Normalize(request.LegalType)?.ToLowerInvariant();
        party.GroupCode = Normalize(request.GroupCode);
        party.ExternalCode = Normalize(request.ExternalCode);
        party.IndustryCode = Normalize(request.IndustryCode);
        party.InvoiceEmail = Normalize(request.InvoiceEmail);
        party.VatRegistered = request.VatRegistered;
        party.AssignedUserId = request.AssignedUserId;
        party.ParentPartyId = request.ParentPartyId;
        if (!string.IsNullOrWhiteSpace(request.CreditControlMode))
        {
            party.CreditControlMode = request.CreditControlMode.Trim().ToLowerInvariant();
        }
    }

    public static async Task EnsureCurrencyExistsAsync(
        ILcmsDbContext db,
        string? defaultCurrencyCode,
        string? creditLimitCurrencyCode,
        CancellationToken cancellationToken)
    {
        foreach (var raw in new[] { defaultCurrencyCode, creditLimitCurrencyCode })
        {
            var code = NormalizeCurrency(raw);
            if (code is null)
            {
                continue;
            }

            var exists = await db.Currencies.AsNoTracking()
                .AnyAsync(c => c.Code == code && c.IsActive, cancellationToken);
            if (!exists)
            {
                throw new ValidationAppException(new Dictionary<string, string[]>
                {
                    ["currencyCode"] = [$"Tiền tệ {code} không tồn tại hoặc đã ngừng dùng."]
                });
            }
        }
    }

    public static bool IsValidVnTaxId(string? taxId)
    {
        if (string.IsNullOrWhiteSpace(taxId))
        {
            return true;
        }

        var digits = new string(taxId.Where(char.IsDigit).ToArray());
        return digits.Length is 10 or 13;
    }

    public static async Task<string> AllocateCodeAsync(
        ILcmsDbContext db,
        Guid tenantId,
        string? requested,
        CancellationToken cancellationToken)
    {
        var code = Normalize(requested);
        if (code is not null)
        {
            return code;
        }

        for (var i = 0; i < 25; i++)
        {
            var candidate = $"DT-{DateTime.UtcNow:yyMMdd}-{Random.Shared.Next(1000, 9999)}";
            var exists = await db.BusinessParties.IgnoreQueryFilters()
                .AnyAsync(p => p.TenantId == tenantId && p.Code == candidate, cancellationToken);
            if (!exists)
            {
                return candidate;
            }
        }

        throw new ConflictAppException("Không cấp được mã đối tác tự động. Hãy nhập mã thủ công.");
    }

    public static async Task EnsureRelationsAsync(
        ILcmsDbContext db,
        Guid tenantId,
        Guid? assignedUserId,
        Guid? parentPartyId,
        Guid? excludePartyId,
        string? externalCode,
        CancellationToken cancellationToken)
    {
        if (assignedUserId is Guid userId)
        {
            var userOk = await db.Users.AsNoTracking().AnyAsync(u => u.Id == userId, cancellationToken);
            if (!userOk)
            {
                throw new NotFoundAppException("Không tìm thấy người phụ trách.");
            }
        }

        if (parentPartyId is Guid parentId)
        {
            if (excludePartyId == parentId)
            {
                throw new ValidationAppException(new Dictionary<string, string[]>
                {
                    ["parentPartyId"] = ["Đối tác không thể là công ty mẹ của chính mình."]
                });
            }

            var parentOk = await db.BusinessParties.AsNoTracking()
                .AnyAsync(p => p.Id == parentId, cancellationToken);
            if (!parentOk)
            {
                throw new NotFoundAppException("Không tìm thấy đối tác mẹ.");
            }
        }

        var ext = Normalize(externalCode);
        if (ext is not null)
        {
            var dup = await db.BusinessParties.IgnoreQueryFilters()
                .AnyAsync(
                    p => p.TenantId == tenantId
                         && p.ExternalCode == ext
                         && p.DeletedAt == null
                         && (excludePartyId == null || p.Id != excludePartyId),
                    cancellationToken);
            if (dup)
            {
                throw new ConflictAppException("Mã đối chiếu ngoài đã tồn tại trên đối tác khác.");
            }
        }
    }
}
