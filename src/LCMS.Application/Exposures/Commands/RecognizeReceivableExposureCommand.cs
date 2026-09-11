using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Exposures.Commands;

/// <summary>
/// Recognize a (partial) slice of receivable exposure into a new accounts_receivable row.
/// Does not create Revenue (C-004). Exposure and AR remain separate records.
/// </summary>
public sealed record RecognizeReceivableExposureCommand(
    Guid ReceivableExposureId,
    decimal Amount,
    DateOnly? DueDate,
    string? Notes) : IRequest<Guid>;

public sealed class RecognizeReceivableExposureCommandValidator
    : AbstractValidator<RecognizeReceivableExposureCommand>
{
    public RecognizeReceivableExposureCommandValidator()
    {
        RuleFor(x => x.ReceivableExposureId).NotEmpty().WithMessage("Exposure phải thu không hợp lệ.");
        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Số tiền ghi nhận phải thu phải lớn hơn 0.");
        RuleFor(x => x.Notes).MaximumLength(2048).When(x => x.Notes is not null);
    }
}

public sealed class RecognizeReceivableExposureCommandHandler
    : IRequestHandler<RecognizeReceivableExposureCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _user;

    public RecognizeReceivableExposureCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext user)
    {
        _db = db;
        _tenantContext = tenantContext;
        _user = user;
    }

    public async Task<Guid> Handle(RecognizeReceivableExposureCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var tenantId = _tenantContext.TenantId!.Value;
        var exposure = await _db.ReceivableExposures
            .FirstOrDefaultAsync(e => e.Id == request.ReceivableExposureId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy quyền thu dự kiến (exposure).");

        if (exposure.Status == ExposureStatuses.Cancelled)
        {
            throw new ConflictAppException("Không thể ghi nhận exposure đã hủy.");
        }

        if (exposure.Status == ExposureStatuses.FullyRecognized)
        {
            throw new ConflictAppException("Exposure phải thu đã được ghi nhận đủ.");
        }

        var amount = decimal.Round(request.Amount, 4, MidpointRounding.AwayFromZero);
        var open = exposure.Amount - exposure.RecognizedAmount;
        if (amount > open)
        {
            throw new ConflictAppException(
                $"Số tiền ghi nhận ({amount}) vượt phần còn lại của exposure ({open}).");
        }

        var costCountBefore = await _db.Costs.CountAsync(cancellationToken);
        var revenueCountBefore = await _db.Revenues.CountAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var ar = new AccountsReceivable
        {
            TenantId = tenantId,
            ReceivableExposureId = exposure.Id,
            BillId = exposure.BillId,
            CounterpartyId = exposure.CounterpartyId,
            RecognizedAmount = amount,
            AdjustmentAmount = 0m,
            FinalizedSettledAmount = 0m,
            CurrencyCode = exposure.CurrencyCode,
            DueDate = request.DueDate ?? exposure.DueDate,
            SettlementStatus = ApArSettlementStatuses.Open,
            RecognizedAt = now,
            RecognizedBy = _user.UserId,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            RecordStatus = "active"
        };

        exposure.RecognizedAmount = decimal.Round(
            exposure.RecognizedAmount + amount, 4, MidpointRounding.AwayFromZero);
        exposure.Status = exposure.RecognizedAmount >= exposure.Amount
            ? ExposureStatuses.FullyRecognized
            : ExposureStatuses.PartiallyRecognized;
        exposure.UpdatedAt = now;
        exposure.UpdatedBy = _user.UserId;

        _db.AccountsReceivable.Add(ar);
        // Explicit: recognition must not invent Cost or Revenue (C-003 / C-004).
        await _db.SaveChangesAsync(cancellationToken);

        var costCountAfter = await _db.Costs.CountAsync(cancellationToken);
        var revenueCountAfter = await _db.Revenues.CountAsync(cancellationToken);
        if (costCountAfter != costCountBefore || revenueCountAfter != revenueCountBefore)
        {
            throw new ConflictAppException(
                "Ghi nhận AR không được tạo Chi phí hoặc Doanh thu mới (C-003/C-004).");
        }

        return ar.Id;
    }
}
