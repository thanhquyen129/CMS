using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Exposures.Commands;

public sealed record CreatePayableExposureCommand(
    decimal Amount,
    string CurrencyCode,
    DateOnly? EffectiveDate,
    DateOnly? DueDate,
    Guid? BillId,
    Guid? CounterpartyId,
    Guid? CostId,
    Guid? FinancialDocumentId,
    string? Notes,
    string? SourceType,
    Guid? SourceId) : IRequest<Guid>;

public sealed class CreatePayableExposureCommandValidator : AbstractValidator<CreatePayableExposureCommand>
{
    public CreatePayableExposureCommandValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Số tiền exposure phải trả phải lớn hơn 0.");
        RuleFor(x => x.CurrencyCode)
            .NotEmpty().WithMessage("Mã tiền tệ không được để trống.")
            .Length(3).WithMessage("Mã tiền tệ phải gồm 3 ký tự.");
        RuleFor(x => x.Notes).MaximumLength(2048).When(x => x.Notes is not null);
        RuleFor(x => x.SourceType).MaximumLength(64).When(x => x.SourceType is not null);
    }
}

public sealed class CreatePayableExposureCommandHandler : IRequestHandler<CreatePayableExposureCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public CreatePayableExposureCommandHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<Guid> Handle(CreatePayableExposureCommand request, CancellationToken cancellationToken)
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

        if (request.CostId.HasValue)
        {
            var costExists = await _db.Costs.AsNoTracking()
                .AnyAsync(c => c.Id == request.CostId, cancellationToken);
            if (!costExists)
            {
                throw new NotFoundAppException("Không tìm thấy chi phí.");
            }
        }

        Guid? billId = request.BillId;
        Guid? counterpartyId = request.CounterpartyId;
        Guid? financialDocumentId = request.FinancialDocumentId;

        if (financialDocumentId.HasValue)
        {
            var doc = await _db.FinancialDocuments.AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == financialDocumentId, cancellationToken)
                ?? throw new NotFoundAppException("Không tìm thấy chứng từ tài chính.");

            if (doc.Direction == FinancialDocumentDirections.Receivable)
            {
                throw new ConflictAppException(
                    "Chứng từ phải thu không thể liên kết với exposure phải trả.");
            }

            billId ??= doc.BillId;
            counterpartyId ??= doc.CounterpartyId;
        }

        var costCountBefore = await _db.Costs.CountAsync(cancellationToken);
        var revenueCountBefore = await _db.Revenues.CountAsync(cancellationToken);

        var exposure = new PayableExposure
        {
            TenantId = tenantId,
            Amount = decimal.Round(request.Amount, 4, MidpointRounding.AwayFromZero),
            RecognizedAmount = 0m,
            CurrencyCode = request.CurrencyCode.Trim().ToUpperInvariant(),
            Status = ExposureStatuses.Open,
            EffectiveDate = request.EffectiveDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
            DueDate = request.DueDate,
            BillId = billId,
            CounterpartyId = counterpartyId,
            CostId = request.CostId,
            FinancialDocumentId = financialDocumentId,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            SourceType = string.IsNullOrWhiteSpace(request.SourceType) ? null : request.SourceType.Trim(),
            SourceId = request.SourceId,
            RecordStatus = "active"
        };

        _db.PayableExposures.Add(exposure);
        await _db.SaveChangesAsync(cancellationToken);

        var costCountAfter = await _db.Costs.CountAsync(cancellationToken);
        var revenueCountAfter = await _db.Revenues.CountAsync(cancellationToken);
        if (costCountAfter != costCountBefore || revenueCountAfter != revenueCountBefore)
        {
            throw new ConflictAppException(
                "Tạo exposure không được tạo Chi phí hoặc Doanh thu mới (C-003/C-004).");
        }

        return exposure.Id;
    }
}
