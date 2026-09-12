using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.DocumentMatches.Commands;

public sealed record CancelDocumentMatchCommand(Guid MatchId, string Reason) : IRequest;

public sealed class CancelDocumentMatchCommandValidator : AbstractValidator<CancelDocumentMatchCommand>
{
    public CancelDocumentMatchCommandValidator()
    {
        RuleFor(x => x.MatchId).NotEmpty().WithMessage("Phiên khớp không hợp lệ.");
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Lý do hủy phiên khớp không được để trống.")
            .MaximumLength(512).WithMessage("Lý do hủy phiên khớp không được vượt quá 512 ký tự.");
    }
}

/// <summary>
/// Cancels a draft/confirmed match session (soft status). Active details must be reversed first.
/// </summary>
public sealed class CancelDocumentMatchCommandHandler : IRequestHandler<CancelDocumentMatchCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _user;

    public CancelDocumentMatchCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext user)
    {
        _db = db;
        _tenantContext = tenantContext;
        _user = user;
    }

    public async Task Handle(CancelDocumentMatchCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var match = await _db.DocumentMatches
            .FirstOrDefaultAsync(m => m.Id == request.MatchId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy phiên khớp chứng từ.");

        if (string.Equals(match.MatchStatus, DocumentMatchStatuses.Cancelled, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictAppException("Phiên khớp đã được hủy.");
        }

        var hasActiveDetails = await _db.DocumentMatchDetails.AsNoTracking()
            .AnyAsync(
                d => d.MatchId == match.Id && d.DetailStatus == DocumentMatchDetailStatuses.Active,
                cancellationToken);
        if (hasActiveDetails)
        {
            throw new ConflictAppException(
                "Phải đảo tất cả chi tiết khớp đang hiệu lực trước khi hủy phiên.");
        }

        match.MatchStatus = DocumentMatchStatuses.Cancelled;
        match.CancelledAt = DateTimeOffset.UtcNow;
        match.CancelledBy = _user.UserId;
        match.CancelReason = request.Reason.Trim();

        await _db.SaveChangesAsync(cancellationToken);
    }
}
