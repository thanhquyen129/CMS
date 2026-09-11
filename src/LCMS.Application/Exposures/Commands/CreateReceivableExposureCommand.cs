using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Exposures.Commands;

public sealed record CreateReceivableExposureCommand(
    decimal Amount,
    string CurrencyCode,
    DateOnly? EffectiveDate,
    DateOnly? DueDate,
    Guid? BillId,
    Guid? CounterpartyId,
    Guid? RevenueId,
    Guid? FinancialDocumentId,
    string? Notes,
    string? SourceType,
    Guid? SourceId) : IRequest<Guid>;

public sealed class CreateReceivableExposureCommandValidator : AbstractValidator<CreateReceivableExposureCommand>
{
    public CreateReceivableExposureCommandValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Số tiền exposure phải thu phải lớn hơn 0.");
        RuleFor(x => x.CurrencyCode)
            .NotEmpty().WithMessage("Mã tiền tệ không được để trống.")
            .Length(3).WithMessage("Mã tiền tệ phải gồm 3 ký tự.");
        RuleFor(x => x.Notes).MaximumLength(2048).When(x => x.Notes is not null);
        RuleFor(x => x.SourceType).MaximumLength(64).When(x => x.SourceType is not null);
    }
}

public sealed class CreateReceivableExposureCommandHandler : IRequestHandler<CreateReceivableExposureCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public CreateReceivableExposureCommandHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<Guid> Handle(CreateReceivableExposureCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var tenantId = _tenantContext.TenantId!.Value;

        if (request.BillId.HasValue)
        {
            var billExists = await _db.Bills.AsNoTracking()
                .AnyAsync(b => b.Id == request.BillId, cancellationToken);
            if (!billExists)
            {
                throw new NotFoundAppException("Không tìm thấy Bill.");
            }
        }

        if (request.RevenueId.HasValue)
        {
            var revenueExists = await _db.Revenues.AsNoTracking()
                .AnyAsync(r => r.Id == request.RevenueId, cancellationToken);
            if (!revenueExists)
            {
                throw new NotFoundAppException("Không tìm thấy doanh thu.");
            }
        }

        if (request.FinancialDocumentId.HasValue)
        {
            var docExists = await _db.FinancialDocuments.AsNoTracking()
                .AnyAsync(d => d.Id == request.FinancialDocumentId, cancellationToken);
            if (!docExists)
            {
                throw new NotFoundAppException("Không tìm thấy chứng từ tài chính.");
            }
        }

        var exposure = new ReceivableExposure
        {
            TenantId = tenantId,
            Amount = decimal.Round(request.Amount, 4, MidpointRounding.AwayFromZero),
            RecognizedAmount = 0m,
            CurrencyCode = request.CurrencyCode.Trim().ToUpperInvariant(),
            Status = ExposureStatuses.Open,
            EffectiveDate = request.EffectiveDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
            DueDate = request.DueDate,
            BillId = request.BillId,
            CounterpartyId = request.CounterpartyId,
            RevenueId = request.RevenueId,
            FinancialDocumentId = request.FinancialDocumentId,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            SourceType = string.IsNullOrWhiteSpace(request.SourceType) ? null : request.SourceType.Trim(),
            SourceId = request.SourceId,
            RecordStatus = "active"
        };

        _db.ReceivableExposures.Add(exposure);
        await _db.SaveChangesAsync(cancellationToken);
        return exposure.Id;
    }
}
