using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Currencies.Commands;

public sealed record UpsertCurrencyCommand(string Code, string Name, int DecimalPlaces, bool IsActive)
    : IRequest<Guid>;

public sealed class UpsertCurrencyCommandValidator : AbstractValidator<UpsertCurrencyCommand>
{
    public UpsertCurrencyCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Mã tiền tệ không được để trống.")
            .Length(3).WithMessage("Mã tiền tệ phải đúng 3 ký tự ISO 4217.")
            .Matches(@"^[A-Za-z]{3}$").WithMessage("Mã tiền tệ phải là 3 chữ cái ISO 4217.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên tiền tệ không được để trống.")
            .MaximumLength(128).WithMessage("Tên tiền tệ không được vượt quá 128 ký tự.");

        RuleFor(x => x.DecimalPlaces)
            .InclusiveBetween(0, 6).WithMessage("Số chữ số thập phân phải từ 0 đến 6.");
    }
}

public sealed class UpsertCurrencyCommandHandler : IRequestHandler<UpsertCurrencyCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService _permissions;

    public UpsertCurrencyCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IPermissionService permissions)
    {
        _db = db;
        _tenantContext = tenantContext;
        _permissions = permissions;
    }

    public async Task<Guid> Handle(UpsertCurrencyCommand request, CancellationToken cancellationToken)
    {
        // Global catalog: permission only when tenant context is present (actor path).
        if (_tenantContext.HasTenant)
        {
            await _permissions.EnsureAsync(
                PermissionCodes.MasterCurrencyManage,
                "Bạn không có quyền quản lý tiền tệ.",
                cancellationToken);
        }

        var code = request.Code.Trim().ToUpperInvariant();

        // Harden: baseline currencies keep fixed decimal places.
        var baseline = CurrencyCatalogSeeder.Baseline.FirstOrDefault(b => b.Code == code);
        var decimalPlaces = baseline.Code is not null ? baseline.DecimalPlaces : request.DecimalPlaces;

        var existing = await _db.Currencies
            .FirstOrDefaultAsync(c => c.Code == code, cancellationToken);

        if (existing is null)
        {
            var currency = new Currency
            {
                Code = code,
                Name = request.Name.Trim(),
                DecimalPlaces = decimalPlaces,
                IsActive = request.IsActive
            };
            _db.Currencies.Add(currency);
            await _db.SaveChangesAsync(cancellationToken);
            return currency.Id;
        }

        if (baseline.Code is not null && request.DecimalPlaces != baseline.DecimalPlaces)
        {
            throw new ValidationAppException(new Dictionary<string, string[]>
            {
                ["DecimalPlaces"] =
                [
                    $"Số chữ số thập phân của {code} phải là {baseline.DecimalPlaces} (không được đổi)."
                ]
            });
        }

        existing.Name = request.Name.Trim();
        existing.DecimalPlaces = decimalPlaces;
        existing.IsActive = request.IsActive;
        await _db.SaveChangesAsync(cancellationToken);
        return existing.Id;
    }
}
