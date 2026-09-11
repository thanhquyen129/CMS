using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
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

    public UpsertCurrencyCommandHandler(ILcmsDbContext db)
    {
        _db = db;
    }

    public async Task<Guid> Handle(UpsertCurrencyCommand request, CancellationToken cancellationToken)
    {
        var code = request.Code.Trim().ToUpperInvariant();

        var existing = await _db.Currencies
            .FirstOrDefaultAsync(c => c.Code == code, cancellationToken);

        if (existing is null)
        {
            var currency = new Currency
            {
                Code = code,
                Name = request.Name.Trim(),
                DecimalPlaces = request.DecimalPlaces,
                IsActive = request.IsActive
            };
            _db.Currencies.Add(currency);
            await _db.SaveChangesAsync(cancellationToken);
            return currency.Id;
        }

        existing.Name = request.Name.Trim();
        existing.DecimalPlaces = request.DecimalPlaces;
        existing.IsActive = request.IsActive;
        await _db.SaveChangesAsync(cancellationToken);
        return existing.Id;
    }
}
