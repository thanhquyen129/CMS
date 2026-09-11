using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Revenues.Commands;

public sealed record CreateRevenueCommand(
    Guid BillId,
    decimal Amount,
    string CurrencyCode,
    DateOnly? EffectiveDate,
    string? RevenueTypeCode,
    Guid? CustomerPartyId,
    string? SourceType,
    Guid? SourceId,
    string? RecognitionPolicyVersion) : IRequest<Guid>;

public sealed class CreateRevenueCommandValidator : AbstractValidator<CreateRevenueCommand>
{
    public CreateRevenueCommandValidator()
    {
        RuleFor(x => x.BillId)
            .NotEmpty().WithMessage("Doanh thu bắt buộc gắn Bill.");
        RuleFor(x => x.Amount)
            .GreaterThanOrEqualTo(0).WithMessage("Số tiền doanh thu không được âm.");
        RuleFor(x => x.CurrencyCode)
            .NotEmpty().WithMessage("Mã tiền tệ không được để trống.")
            .Length(3).WithMessage("Mã tiền tệ phải gồm 3 ký tự.");
        RuleFor(x => x.RevenueTypeCode).MaximumLength(64).When(x => x.RevenueTypeCode is not null);
        RuleFor(x => x.SourceType).MaximumLength(64).When(x => x.SourceType is not null);
        RuleFor(x => x.RecognitionPolicyVersion).MaximumLength(64).When(x => x.RecognitionPolicyVersion is not null);
        // C-004: document/AR must not invent a second economic revenue row.
        RuleFor(x => x.SourceType)
            .Must(s => s is null
                       || (!string.Equals(s, RevenueSourceTypes.Document, StringComparison.OrdinalIgnoreCase)
                           && !string.Equals(s, RevenueSourceTypes.AccountsReceivable, StringComparison.OrdinalIgnoreCase)))
            .WithMessage("Không tạo doanh thu kinh tế mới chỉ từ chứng từ/AR (C-004).");
    }
}

public sealed class CreateRevenueCommandHandler : IRequestHandler<CreateRevenueCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public CreateRevenueCommandHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<Guid> Handle(CreateRevenueCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var tenantId = _tenantContext.TenantId!.Value;
        var currency = request.CurrencyCode.Trim().ToUpperInvariant();
        var amount = decimal.Round(request.Amount, 4, MidpointRounding.AwayFromZero);

        var bill = await _db.Bills.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == request.BillId, cancellationToken);
        if (bill is null)
        {
            throw new NotFoundAppException("Không tìm thấy Bill.");
        }

        if (request.SourceId.HasValue && !string.IsNullOrWhiteSpace(request.SourceType))
        {
            var existing = await _db.Revenues.AsNoTracking()
                .FirstOrDefaultAsync(
                    r => r.SourceType == request.SourceType && r.SourceId == request.SourceId,
                    cancellationToken);
            if (existing is not null)
            {
                return existing.Id;
            }
        }

        var revenue = new Revenue
        {
            TenantId = tenantId,
            BillId = request.BillId,
            FinancialMaturity = RevenueMaturities.Expected,
            ExpectedAmount = amount,
            Amount = amount,
            CurrencyCode = currency,
            RevenueTypeCode = string.IsNullOrWhiteSpace(request.RevenueTypeCode) ? null : request.RevenueTypeCode.Trim(),
            CustomerPartyId = request.CustomerPartyId,
            SourceType = string.IsNullOrWhiteSpace(request.SourceType) ? RevenueSourceTypes.Manual : request.SourceType.Trim(),
            SourceId = request.SourceId,
            RecognitionPolicyVersion = string.IsNullOrWhiteSpace(request.RecognitionPolicyVersion)
                ? null
                : request.RecognitionPolicyVersion.Trim(),
            RecordStatus = "active",
            ApprovalStatus = "not_required",
            EffectiveDate = request.EffectiveDate ?? DateOnly.FromDateTime(DateTime.UtcNow)
        };

        _db.Revenues.Add(revenue);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            if (request.SourceId.HasValue)
            {
                var again = await _db.Revenues.AsNoTracking()
                    .FirstOrDefaultAsync(
                        r => r.SourceType == request.SourceType && r.SourceId == request.SourceId,
                        cancellationToken);
                if (again is not null)
                {
                    return again.Id;
                }
            }

            throw new ConflictAppException("Không thể tạo doanh thu do xung đột dữ liệu.");
        }

        return revenue.Id;
    }
}
