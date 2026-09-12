using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Exposures.Commands;

/// <summary>
/// Optional link: financial document → payable exposure (C-003: never invents Cost).
/// </summary>
public sealed record LinkDocumentToPayableExposureCommand(
    Guid PayableExposureId,
    Guid FinancialDocumentId) : IRequest;

public sealed class LinkDocumentToPayableExposureCommandValidator
    : AbstractValidator<LinkDocumentToPayableExposureCommand>
{
    public LinkDocumentToPayableExposureCommandValidator()
    {
        RuleFor(x => x.PayableExposureId).NotEmpty().WithMessage("Exposure phải trả không hợp lệ.");
        RuleFor(x => x.FinancialDocumentId).NotEmpty().WithMessage("Chứng từ tài chính không hợp lệ.");
    }
}

public sealed class LinkDocumentToPayableExposureCommandHandler
    : IRequestHandler<LinkDocumentToPayableExposureCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _user;

    public LinkDocumentToPayableExposureCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext user)
    {
        _db = db;
        _tenantContext = tenantContext;
        _user = user;
    }

    public async Task Handle(LinkDocumentToPayableExposureCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var exposure = await _db.PayableExposures
            .FirstOrDefaultAsync(e => e.Id == request.PayableExposureId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy nghĩa vụ phải trả (exposure).");

        if (exposure.Status == ExposureStatuses.Cancelled)
        {
            throw new ConflictAppException("Không thể liên kết chứng từ với exposure đã hủy.");
        }

        var document = await _db.FinancialDocuments.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == request.FinancialDocumentId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy chứng từ tài chính.");

        if (document.Direction == FinancialDocumentDirections.Receivable)
        {
            throw new ConflictAppException(
                "Chứng từ phải thu không thể liên kết với exposure phải trả.");
        }

        var costCountBefore = await _db.Costs.CountAsync(cancellationToken);
        var revenueCountBefore = await _db.Revenues.CountAsync(cancellationToken);

        exposure.FinancialDocumentId = document.Id;
        if (!exposure.BillId.HasValue && document.BillId.HasValue)
        {
            exposure.BillId = document.BillId;
        }

        if (!exposure.CounterpartyId.HasValue && document.CounterpartyId.HasValue)
        {
            exposure.CounterpartyId = document.CounterpartyId;
        }

        exposure.UpdatedAt = DateTimeOffset.UtcNow;
        exposure.UpdatedBy = _user.UserId;
        exposure.TouchRowVersion();

        await _db.SaveChangesAsync(cancellationToken);

        var costCountAfter = await _db.Costs.CountAsync(cancellationToken);
        var revenueCountAfter = await _db.Revenues.CountAsync(cancellationToken);
        if (costCountAfter != costCountBefore || revenueCountAfter != revenueCountBefore)
        {
            throw new ConflictAppException(
                "Liên kết chứng từ với exposure không được tạo Chi phí hoặc Doanh thu mới (C-003/C-004).");
        }
    }
}
