using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.PartyBankAccounts.Commands;

public sealed record UpsertPartyBankAccountCommand(
    Guid PartyId,
    Guid? Id,
    string BankName,
    string? BankBranch,
    string? BankCode,
    string? SwiftBic,
    string AccountNumber,
    string? AccountName,
    string CurrencyCode,
    bool IsDefault,
    bool IsActive,
    string? Note) : IRequest<Guid>;

public sealed class UpsertPartyBankAccountCommandValidator : AbstractValidator<UpsertPartyBankAccountCommand>
{
    public UpsertPartyBankAccountCommandValidator()
    {
        RuleFor(x => x.PartyId).NotEmpty().WithMessage("Đối tác không hợp lệ.");
        RuleFor(x => x.BankName)
            .NotEmpty().WithMessage("Tên ngân hàng không được để trống.")
            .MaximumLength(256);
        RuleFor(x => x.BankBranch).MaximumLength(256);
        RuleFor(x => x.BankCode).MaximumLength(32);
        RuleFor(x => x.SwiftBic).MaximumLength(16);
        RuleFor(x => x.AccountNumber)
            .NotEmpty().WithMessage("Số tài khoản không được để trống.")
            .MaximumLength(64);
        RuleFor(x => x.AccountName).MaximumLength(256);
        RuleFor(x => x.CurrencyCode)
            .NotEmpty().WithMessage("Mã tiền tệ tài khoản không được để trống.")
            .Length(3).WithMessage("Mã tiền tệ phải đúng 3 ký tự ISO 4217.")
            .Matches(@"^[A-Za-z]{3}$");
        RuleFor(x => x.Note).MaximumLength(512);
    }
}

public sealed class UpsertPartyBankAccountCommandHandler : IRequestHandler<UpsertPartyBankAccountCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService _permissions;

    public UpsertPartyBankAccountCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IPermissionService permissions)
    {
        _db = db;
        _tenantContext = tenantContext;
        _permissions = permissions;
    }

    public async Task<Guid> Handle(UpsertPartyBankAccountCommand request, CancellationToken cancellationToken)
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
        var party = await _db.BusinessParties.FirstOrDefaultAsync(p => p.Id == request.PartyId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy đối tác.");

        var currency = request.CurrencyCode.Trim().ToUpperInvariant();
        if (!await _db.Currencies.AsNoTracking().AnyAsync(c => c.Code == currency && c.IsActive, cancellationToken))
        {
            throw new ValidationAppException(new Dictionary<string, string[]>
            {
                ["currencyCode"] = [$"Tiền tệ {currency} không tồn tại hoặc đã ngừng dùng."]
            });
        }

        var accountNumber = request.AccountNumber.Trim();
        PartyBankAccount row;
        if (request.Id is Guid id)
        {
            row = await _db.PartyBankAccounts.FirstOrDefaultAsync(
                    b => b.Id == id && b.PartyId == party.Id,
                    cancellationToken)
                ?? throw new NotFoundAppException("Không tìm thấy tài khoản ngân hàng đối tác.");
        }
        else
        {
            row = new PartyBankAccount
            {
                TenantId = tenantId,
                PartyId = party.Id
            };
            _db.PartyBankAccounts.Add(row);
        }

        var duplicate = await _db.PartyBankAccounts.AnyAsync(
            b => b.PartyId == party.Id
                 && b.AccountNumber == accountNumber
                 && b.Id != row.Id,
            cancellationToken);
        if (duplicate)
        {
            throw new ConflictAppException("Số tài khoản đã tồn tại trên đối tác này.");
        }

        row.BankName = request.BankName.Trim();
        row.BankBranch = string.IsNullOrWhiteSpace(request.BankBranch) ? null : request.BankBranch.Trim();
        row.BankCode = string.IsNullOrWhiteSpace(request.BankCode) ? null : request.BankCode.Trim().ToUpperInvariant();
        row.SwiftBic = string.IsNullOrWhiteSpace(request.SwiftBic) ? null : request.SwiftBic.Trim().ToUpperInvariant();
        row.AccountNumber = accountNumber;
        row.AccountName = string.IsNullOrWhiteSpace(request.AccountName) ? null : request.AccountName.Trim();
        row.CurrencyCode = currency;
        row.IsDefault = request.IsDefault;
        row.IsActive = request.IsActive;
        row.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();

        if (row.IsDefault)
        {
            var others = await _db.PartyBankAccounts
                .Where(b => b.PartyId == party.Id && b.Id != row.Id && b.IsDefault)
                .ToListAsync(cancellationToken);
            foreach (var other in others)
            {
                other.IsDefault = false;
            }
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new ConflictAppException("Số tài khoản đã tồn tại trên đối tác này.");
        }

        return row.Id;
    }
}

public sealed record SoftDeletePartyBankAccountCommand(Guid PartyId, Guid BankAccountId) : IRequest;

public sealed class SoftDeletePartyBankAccountCommandHandler : IRequestHandler<SoftDeletePartyBankAccountCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService _permissions;
    private readonly ICurrentUserContext _userContext;

    public SoftDeletePartyBankAccountCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IPermissionService permissions,
        ICurrentUserContext userContext)
    {
        _db = db;
        _tenantContext = tenantContext;
        _permissions = permissions;
        _userContext = userContext;
    }

    public async Task Handle(SoftDeletePartyBankAccountCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        await _permissions.EnsureAsync(
            PermissionCodes.MasterPartyManage,
            "Bạn không có quyền quản lý đối tác.",
            cancellationToken);

        var row = await _db.PartyBankAccounts.FirstOrDefaultAsync(
                b => b.Id == request.BankAccountId && b.PartyId == request.PartyId,
                cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy tài khoản ngân hàng đối tác.");

        row.SoftDelete(_userContext.UserId);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
