using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Fx.Commands;

public sealed record UpsertFxRateCommand(
    string FromCurrencyCode,
    string ToCurrencyCode,
    DateOnly RateDate,
    decimal Rate,
    string? Source,
    int? Version,
    string? Note) : IRequest<Guid>;

public sealed class UpsertFxRateCommandValidator : AbstractValidator<UpsertFxRateCommand>
{
    public UpsertFxRateCommandValidator()
    {
        RuleFor(x => x.FromCurrencyCode)
            .NotEmpty().WithMessage("Mã tiền tệ nguồn không được để trống.")
            .Length(3).WithMessage("Mã tiền tệ nguồn phải gồm 3 ký tự.");
        RuleFor(x => x.ToCurrencyCode)
            .NotEmpty().WithMessage("Mã tiền tệ đích không được để trống.")
            .Length(3).WithMessage("Mã tiền tệ đích phải gồm 3 ký tự.");
        RuleFor(x => x.Rate)
            .GreaterThan(0).WithMessage("Tỷ giá phải lớn hơn 0.");
        RuleFor(x => x.Version)
            .GreaterThan(0)
            .When(x => x.Version.HasValue)
            .WithMessage("Phiên bản tỷ giá phải lớn hơn 0.");
        RuleFor(x => x.Source)
            .MaximumLength(64)
            .When(x => x.Source is not null);
        RuleFor(x => x.Note)
            .MaximumLength(512)
            .When(x => x.Note is not null);
        RuleFor(x => x)
            .Must(x => !string.Equals(
                x.FromCurrencyCode.Trim(),
                x.ToCurrencyCode.Trim(),
                StringComparison.OrdinalIgnoreCase))
            .WithMessage("Tiền tệ nguồn và đích phải khác nhau.");
    }
}

public sealed class UpsertFxRateCommandHandler : IRequestHandler<UpsertFxRateCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public UpsertFxRateCommandHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<Guid> Handle(UpsertFxRateCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var tenantId = _tenantContext.TenantId!.Value;
        var from = request.FromCurrencyCode.Trim().ToUpperInvariant();
        var to = request.ToCurrencyCode.Trim().ToUpperInvariant();
        var version = request.Version is > 0 ? request.Version.Value : 1;
        var source = string.IsNullOrWhiteSpace(request.Source)
            ? FxRateSources.Manual
            : request.Source.Trim().ToLowerInvariant();
        var rate = decimal.Round(request.Rate, 8, MidpointRounding.AwayFromZero);

        var existing = await _db.FxRates.FirstOrDefaultAsync(
            r => r.FromCurrencyCode == from
                 && r.ToCurrencyCode == to
                 && r.RateDate == request.RateDate
                 && r.Version == version,
            cancellationToken);

        if (existing is not null)
        {
            existing.Rate = rate;
            existing.Source = source;
            existing.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
            await _db.SaveChangesAsync(cancellationToken);
            return existing.Id;
        }

        var row = new FxRate
        {
            TenantId = tenantId,
            FromCurrencyCode = from,
            ToCurrencyCode = to,
            RateDate = request.RateDate,
            Rate = rate,
            Source = source,
            Version = version,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim()
        };

        _db.FxRates.Add(row);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new ConflictAppException("Tỷ giá đã tồn tại cho cặp tiền / ngày / phiên bản này.");
        }

        return row.Id;
    }
}

public sealed record SoftDeleteFxRateCommand(Guid Id) : IRequest;

public sealed class SoftDeleteFxRateCommandHandler : IRequestHandler<SoftDeleteFxRateCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public SoftDeleteFxRateCommandHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task Handle(SoftDeleteFxRateCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var row = await _db.FxRates.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);
        if (row is null)
        {
            throw new NotFoundAppException("Không tìm thấy tỷ giá.");
        }

        row.SoftDelete(null);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
