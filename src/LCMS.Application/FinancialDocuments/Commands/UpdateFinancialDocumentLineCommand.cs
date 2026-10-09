using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Audit;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using LCMS.Domain.Pricing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.FinancialDocuments.Commands;

public sealed record UpdateFinancialDocumentLineCommand(
    Guid DocumentId,
    Guid LineId,
    decimal Amount,
    string? Description,
    Guid? BillId,
    string? CostTypeCode,
    string? RevenueTypeCode,
    bool UpdateVat = false,
    decimal? NetAmount = null,
    decimal? VatRate = null,
    Guid? RatingDetailId = null) : IRequest;

public sealed class UpdateFinancialDocumentLineCommandValidator : AbstractValidator<UpdateFinancialDocumentLineCommand>
{
    public UpdateFinancialDocumentLineCommandValidator()
    {
        RuleFor(x => x.DocumentId).NotEmpty().WithMessage("Chứng từ không hợp lệ.");
        RuleFor(x => x.LineId).NotEmpty().WithMessage("Dòng chứng từ không hợp lệ.");
        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Số tiền dòng chứng từ phải lớn hơn 0.");
        RuleFor(x => x.Description).MaximumLength(512).When(x => x.Description is not null);
        RuleFor(x => x.CostTypeCode).MaximumLength(64).When(x => x.CostTypeCode is not null);
        RuleFor(x => x.RevenueTypeCode).MaximumLength(64).When(x => x.RevenueTypeCode is not null);
        RuleFor(x => x.VatRate).Must(DeclaredVat.IsValid).WithMessage("Thuế suất VAT phải từ 0 đến 100, hoặc để trống nếu chưa khai báo.");
        RuleFor(x => x).Must(x => !x.UpdateVat || x.NetAmount is null == x.VatRate is null)
            .WithMessage("Khai tiền trước VAT cùng thuế suất, hoặc để trống cả hai.");
    }
}

public sealed class UpdateFinancialDocumentLineCommandHandler : IRequestHandler<UpdateFinancialDocumentLineCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IAuditWriter _audit;

    public UpdateFinancialDocumentLineCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IAuditWriter audit)
    {
        _db = db;
        _tenantContext = tenantContext;
        _audit = audit;
    }

    public async Task Handle(UpdateFinancialDocumentLineCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var document = await _db.FinancialDocuments
            .FirstOrDefaultAsync(d => d.Id == request.DocumentId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy chứng từ tài chính.");

        DocumentLineIntegrity.EnsureDocumentAllowsLineDraft(document);

        var line = await _db.FinancialDocumentLines
            .FirstOrDefaultAsync(l => l.Id == request.LineId && l.DocumentId == document.Id, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy dòng chứng từ.");

        DocumentLineIntegrity.EnsureLineUnmatched(line);

        var hasMatchHistory = await _db.DocumentMatchDetails.AsNoTracking()
            .AnyAsync(
                d => d.SourceLineId == line.Id || d.TargetLineId == line.Id,
                cancellationToken);
        if (hasMatchHistory)
        {
            throw new ConflictAppException("Dòng đã từng tham gia khớp — không sửa số/xóa; chỉ hủy chi tiết khớp.");
        }

        var amount = DocumentLineIntegrity.RoundMoney(request.Amount);
        var accepted = DocumentLineIntegrity.IsAccepted(document);

        if (accepted && amount != line.Amount)
        {
            throw new ConflictAppException("Chứng từ đã chấp nhận — không đổi số tiền dòng (ADR-0012).");
        }

        if (request.BillId.HasValue)
        {
            var billExists = await _db.Bills.AsNoTracking()
                .AnyAsync(b => b.Id == request.BillId, cancellationToken);
            if (!billExists)
            {
                throw new NotFoundAppException("Không tìm thấy Bill.");
            }
        }

        if (!accepted)
        {
            var otherSum = await DocumentLineIntegrity.SumLineAmountsAsync(
                _db.FinancialDocumentLines, document.Id, cancellationToken, excludeLineId: line.Id);
            DocumentLineIntegrity.EnsureSumDoesNotExceedHeader(
                otherSum + amount, document.TotalAmount, document.CurrencyCode);
        }

        var beforeJson = AuditJson.Serialize(new
        {
            id = line.Id,
            documentId = document.Id,
            lineNo = line.LineNo,
            amount = line.Amount,
            description = line.Description,
            billId = line.BillId,
            costTypeCode = line.CostTypeCode,
            revenueTypeCode = line.RevenueTypeCode
        });

        line.Amount = amount;
        line.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        line.BillId = request.BillId ?? document.BillId;
        line.CostTypeCode = string.IsNullOrWhiteSpace(request.CostTypeCode) ? null : request.CostTypeCode.Trim();
        line.RevenueTypeCode = string.IsNullOrWhiteSpace(request.RevenueTypeCode) ? null : request.RevenueTypeCode.Trim();
        if (request.UpdateVat)
        {
            if (request.NetAmount is decimal net && request.VatRate is decimal rate)
            {
                var split = DeclaredVat.Split(net, rate);
                line.NetAmount = split.Net;
                line.VatRate = rate;
                line.VatAmount = split.Vat;
                line.GrossAmount = split.Gross;
                line.RatingDetailId = request.RatingDetailId ?? line.RatingDetailId;
                if (line.RatingDetailId is Guid ratingDetailId)
                {
                    var snapshot = await _db.RatingDetails.AsNoTracking()
                        .FirstOrDefaultAsync(d => d.Id == ratingDetailId, cancellationToken)
                        ?? throw new NotFoundAppException("Không tìm thấy dòng tính giá để đối soát VAT.");
                    line.VatVarianceAmount = split.Vat is decimal documentVat && snapshot.VatAmount is decimal rated
                        ? decimal.Round(documentVat - rated, 4, MidpointRounding.AwayFromZero)
                        : null;
                }
            }
            else
            {
                line.NetAmount = null;
                line.VatRate = null;
                line.VatAmount = null;
                line.GrossAmount = null;
                line.VatVarianceAmount = null;
            }
        }

        _audit.Append(
            AuditActions.FinancialDocumentLineUpdate,
            AuditObjectTypes.FinancialDocumentLine,
            line.Id,
            beforeJson: beforeJson,
            afterJson: AuditJson.Serialize(new
            {
                id = line.Id,
                documentId = document.Id,
                lineNo = line.LineNo,
                amount = line.Amount,
                description = line.Description,
                billId = line.BillId,
                costTypeCode = line.CostTypeCode,
                revenueTypeCode = line.RevenueTypeCode
            }));

        await _db.SaveChangesAsync(cancellationToken);
    }
}
