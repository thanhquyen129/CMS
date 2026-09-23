using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.Ratings;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.RateCards.Commands;

public sealed record ComposeTariffColumn(string Code, string Name);

public sealed record ComposeTariffBand(decimal MinQuantity, decimal? MaxQuantity, IReadOnlyList<decimal?> Prices);

public sealed record ComposeDeliveryFee(decimal UnderQuantity, decimal Amount);

public sealed record ComposeRemoteFee(decimal AmountPerKg, string CurrencyCode, IReadOnlyList<string> Destinations);

public sealed record ComposeTariffResult(Guid RateCardId, Guid RateVersionId);

/// <summary>
/// Creates a draft rate card (or fills an empty draft version) as a weight-band matrix:
/// one column per commodity, one break per priced cell. Empty cells are not a price.
/// </summary>
public sealed record ComposeTariffCommand(
    string? Code,
    string? Name,
    string? PartyType,
    string? CurrencyCode,
    string? Description,
    string? TransportMode,
    string? RouteCode,
    string? CarrierName,
    DateTimeOffset? EffectiveFrom,
    string? Note,
    Guid? RateVersionId,
    decimal? MinimumQuantity,
    IReadOnlyList<ComposeTariffColumn> Columns,
    IReadOnlyList<ComposeTariffBand> Bands,
    ComposeDeliveryFee? Delivery,
    ComposeRemoteFee? Remote) : IRequest<ComposeTariffResult>;

public sealed class ComposeTariffCommandValidator : AbstractValidator<ComposeTariffCommand>
{
    public ComposeTariffCommandValidator()
    {
        RuleFor(x => x.Columns).NotEmpty().WithMessage("Thêm ít nhất một cột loại hàng.");
        RuleFor(x => x.Bands).NotEmpty().WithMessage("Thêm ít nhất một khung khối.");
        RuleFor(x => x.Code).NotEmpty().When(x => x.RateVersionId is null).WithMessage("Nhập mã bảng giá.");
        RuleFor(x => x.Name).NotEmpty().When(x => x.RateVersionId is null).WithMessage("Nhập tên bảng giá.");
        RuleFor(x => x.PartyType)
            .Must(p => p is "customer" or "vendor")
            .When(x => x.RateVersionId is null)
            .WithMessage("Loại giá phải là giá mua hoặc giá bán.");
        RuleFor(x => x.CurrencyCode)
            .Length(3)
            .When(x => x.RateVersionId is null)
            .WithMessage("Mã tiền tệ phải gồm 3 ký tự.");
        RuleFor(x => x.MinimumQuantity)
            .GreaterThan(0)
            .When(x => x.MinimumQuantity.HasValue)
            .WithMessage("Số lượng tối thiểu phải lớn hơn 0.");
        RuleForEach(x => x.Columns).ChildRules(col =>
        {
            col.RuleFor(c => c.Code).NotEmpty().MaximumLength(64).WithMessage("Mã loại hàng không hợp lệ.");
            col.RuleFor(c => c.Name).NotEmpty().MaximumLength(256).WithMessage("Tên loại hàng không hợp lệ.");
        });
    }
}

public sealed class ComposeTariffCommandHandler : IRequestHandler<ComposeTariffCommand, ComposeTariffResult>
{
    private const decimal Gap = 0.001m;

    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService _permissions;

    public ComposeTariffCommandHandler(ILcmsDbContext db, ITenantContext tenantContext, IPermissionService permissions)
    {
        _db = db;
        _tenantContext = tenantContext;
        _permissions = permissions;
    }

    public async Task<ComposeTariffResult> Handle(ComposeTariffCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var tenantId = _tenantContext.TenantId!.Value;
        var columns = request.Columns
            .Select(c => new ComposeTariffColumn(c.Code.Trim().ToUpperInvariant(), c.Name.Trim()))
            .ToList();
        if (columns.Select(c => c.Code).Distinct(StringComparer.OrdinalIgnoreCase).Count() != columns.Count)
        {
            throw new ConflictAppException("Mã loại hàng bị trùng.");
        }

        foreach (var band in request.Bands)
        {
            if (band.Prices.Count != columns.Count)
            {
                throw new ConflictAppException("Mỗi khung phải có một ô giá cho từng loại hàng.");
            }

            if (band.Prices.Any(p => p is < 0))
            {
                throw new ConflictAppException("Đơn giá không được âm.");
            }
        }

        if (!request.Bands.Any(b => b.Prices.Any(p => p is not null)))
        {
            throw new ConflictAppException("Nhập ít nhất một đơn giá.");
        }

        var expanded = Expand(request.Bands);
        RateCard card;
        RateVersion version;

        if (request.RateVersionId is Guid versionId)
        {
            version = await _db.RateVersions.FirstOrDefaultAsync(v => v.Id == versionId, cancellationToken)
                ?? throw new NotFoundAppException("Không tìm thấy phiên bản bảng giá.");
            if (version.IsPublished)
            {
                throw new ConflictAppException("Phiên bản đã phát hành không được sửa. Hãy tạo phiên bản mới.");
            }

            card = await _db.RateCards.FirstOrDefaultAsync(c => c.Id == version.RateCardId, cancellationToken)
                ?? throw new NotFoundAppException("Không tìm thấy bảng giá.");
            await EnsureWriteAsync(card.PartyType, cancellationToken);
        }
        else
        {
            var partyType = request.PartyType!.Trim().ToLowerInvariant();
            await EnsureWriteAsync(partyType, cancellationToken);
            var code = request.Code!.Trim();
            if (await _db.RateCards.AnyAsync(r => r.TenantId == tenantId && r.Code == code, cancellationToken))
            {
                throw new ConflictAppException("Mã bảng giá đã tồn tại trong thuê bao này.");
            }

            card = new RateCard
            {
                TenantId = tenantId,
                Code = code,
                Name = request.Name!.Trim(),
                PartyType = partyType,
                CurrencyCode = request.CurrencyCode!.Trim().ToUpperInvariant(),
                Description = Clean(request.Description),
                TransportMode = Clean(request.TransportMode)?.ToLowerInvariant(),
                RouteCode = Clean(request.RouteCode),
                CarrierName = Clean(request.CarrierName),
                IsActive = true
            };
            version = new RateVersion
            {
                TenantId = tenantId,
                RateCardId = card.Id,
                VersionNo = 1,
                Status = RateVersionStatuses.Draft,
                EffectiveFrom = request.EffectiveFrom,
                Note = Clean(request.Note)
            };
            _db.RateCards.Add(card);
            _db.RateVersions.Add(version);
        }

        var mode = Clean(request.TransportMode) ?? card.TransportMode;
        var air = mode is not null && mode.Equals("air", StringComparison.OrdinalIgnoreCase);
        var sea = mode is not null && (mode.Equals("sea", StringComparison.OrdinalIgnoreCase) || mode.Equals("ocean", StringComparison.OrdinalIgnoreCase));
        var currency = card.CurrencyCode;

        for (var col = 0; col < columns.Count; col++)
        {
            var prices = expanded.Select(b => b.Prices[col]).ToList();
            if (prices.All(p => p is null))
            {
                continue;
            }

            var minAmount = MinimumCharge(request.MinimumQuantity, expanded, col);
            var rule = new PricingRule
            {
                TenantId = tenantId,
                RateVersionId = version.Id,
                Code = columns[col].Code,
                Name = columns[col].Name,
                CalcMethod = PricingCalcMethods.WeightBreakPivot,
                UnitAmount = 0m,
                CurrencyCode = currency,
                ChargeCode = ReferenceCharge.Freight,
                CommodityCode = columns[col].Code,
                MinAmount = minAmount,
                VolumetricFactor = air ? RatingEngine.DefaultAirFactor : null,
                SortOrder = (col + 1) * 10,
                IsActive = true
            };
            _db.PricingRules.Add(rule);
            var seq = 1;
            for (var row = 0; row < expanded.Count; row++)
            {
                if (expanded[row].Prices[col] is not decimal amount)
                {
                    continue;
                }

                _db.RateBreaks.Add(new RateBreak
                {
                    TenantId = tenantId,
                    PricingRuleId = rule.Id,
                    SequenceNo = seq++,
                    MinQuantity = expanded[row].Min,
                    MaxQuantity = expanded[row].Max,
                    UnitAmount = amount
                });
            }
        }

        if (request.Delivery is ComposeDeliveryFee delivery)
        {
            if (delivery.UnderQuantity <= 0 || delivery.Amount < 0)
            {
                throw new ConflictAppException("Phí giao hàng không hợp lệ.");
            }

            var rule = new PricingRule
            {
                TenantId = tenantId,
                RateVersionId = version.Id,
                Code = "DELIVERY",
                Name = $"Phí giao hàng dưới {delivery.UnderQuantity} đơn vị",
                CalcMethod = PricingCalcMethods.WeightStep,
                UnitAmount = 0m,
                CurrencyCode = currency,
                ChargeCode = ReferenceCharge.Delivery,
                SortOrder = 50,
                IsActive = true
            };
            _db.PricingRules.Add(rule);
            var cap = delivery.UnderQuantity - Gap;
            _db.RateBreaks.Add(new RateBreak
            {
                TenantId = tenantId,
                PricingRuleId = rule.Id,
                SequenceNo = 1,
                MinQuantity = 0m,
                MaxQuantity = cap > 0 ? cap : 0m,
                UnitAmount = delivery.Amount
            });
            _db.RateBreaks.Add(new RateBreak
            {
                TenantId = tenantId,
                PricingRuleId = rule.Id,
                SequenceNo = 2,
                MinQuantity = delivery.UnderQuantity,
                MaxQuantity = null,
                UnitAmount = 0m
            });
        }

        if (request.Remote is ComposeRemoteFee remote)
        {
            var destinations = remote.Destinations
                .Select(d => d.Trim().ToUpperInvariant())
                .Where(d => d.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (destinations.Count == 0 || remote.AmountPerKg < 0)
            {
                throw new ConflictAppException("Phụ phí vùng cần đơn giá và ít nhất một mã điểm đến.");
            }

            var remoteCurrency = string.IsNullOrWhiteSpace(remote.CurrencyCode)
                ? currency
                : remote.CurrencyCode.Trim().ToUpperInvariant();
            if (remoteCurrency.Length != 3)
            {
                throw new ConflictAppException("Tiền tệ phụ phí vùng phải gồm 3 ký tự.");
            }

            var sort = 60;
            foreach (var destination in destinations)
            {
                _db.PricingRules.Add(new PricingRule
                {
                    TenantId = tenantId,
                    RateVersionId = version.Id,
                    Code = "REMOTE-" + destination,
                    Name = "Phụ phí vùng " + destination,
                    CalcMethod = PricingCalcMethods.UnitRate,
                    UnitAmount = remote.AmountPerKg,
                    CurrencyCode = remoteCurrency,
                    ChargeCode = ReferenceCharge.Remote,
                    DestinationCode = destination,
                    Applicability = sea ? RatingEngine.PerGrossKg : null,
                    SortOrder = sort++,
                    IsActive = true
                });
            }
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new ConflictAppException("Không lưu được bảng giá. Mã bảng hoặc mã loại hàng đã tồn tại trên phiên bản này.");
        }

        return new ComposeTariffResult(card.Id, version.Id);
    }

    private async Task EnsureWriteAsync(string partyType, CancellationToken cancellationToken)
    {
        var writeCode = string.Equals(partyType, "customer", StringComparison.OrdinalIgnoreCase)
            ? PermissionCodes.RateSellWrite
            : PermissionCodes.RateBuyWrite;
        await _permissions.EnsureAsync(writeCode, "Bạn không có quyền sửa bảng giá này.", cancellationToken);
    }

    /// <summary>
    /// When the next band starts at this band's "đến", pull the max back by 0.001
    /// so 10.5 stays in 6–10 and the bands do not overlap.
    /// </summary>
    internal static IReadOnlyList<ExpandedBand> Expand(IReadOnlyList<ComposeTariffBand> bands)
    {
        var ordered = bands
            .Select(b => new ExpandedBand(b.MinQuantity, b.MaxQuantity, b.Prices.ToArray()))
            .OrderBy(b => b.Min)
            .ToList();
        if (ordered.Any(b => b.Min < 0))
        {
            throw new ConflictAppException("Mức từ của khung không được âm.");
        }

        for (var i = 0; i < ordered.Count; i++)
        {
            var current = ordered[i];
            if (current.Max is decimal max && max < current.Min)
            {
                throw new ConflictAppException("Mức đến của khung phải lớn hơn hoặc bằng mức từ.");
            }

            if (i == ordered.Count - 1)
            {
                continue;
            }

            var nextMin = ordered[i + 1].Min;
            var adjusted = current.Max;
            if (adjusted is null || nextMin - adjusted.Value <= 1m)
            {
                adjusted = nextMin - Gap;
            }

            if (adjusted >= nextMin || adjusted < current.Min)
            {
                throw new ConflictAppException("Các khung khối đang chồng nhau.");
            }

            ordered[i] = current with { Max = adjusted };
        }

        return ordered;
    }

    private static decimal? MinimumCharge(decimal? minimumQuantity, IReadOnlyList<ExpandedBand> bands, int column)
    {
        if (minimumQuantity is not decimal qty)
        {
            return null;
        }

        var hit = bands.FirstOrDefault(b => qty >= b.Min && (b.Max is null || qty <= b.Max));
        if (hit?.Prices[column] is not decimal rate)
        {
            return null;
        }

        return decimal.Round(qty * rate, 4, MidpointRounding.AwayFromZero);
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record ExpandedBand(decimal Min, decimal? Max, decimal?[] Prices);

file static class ReferenceCharge
{
    public const string Freight = "FREIGHT";
    public const string Delivery = "DELIVERY";
    public const string Remote = "REMOTE";
}
