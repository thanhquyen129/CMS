using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.BankFeed.Commands;

public sealed record IgnoreBankFeedLineCommand(Guid Id, string? IgnoreReason) : IRequest;

public sealed class IgnoreBankFeedLineCommandValidator : AbstractValidator<IgnoreBankFeedLineCommand>
{
    public IgnoreBankFeedLineCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Dòng ngân hàng không hợp lệ.");
        RuleFor(x => x.IgnoreReason).MaximumLength(2048).When(x => x.IgnoreReason is not null);
    }
}

public sealed class IgnoreBankFeedLineCommandHandler : IRequestHandler<IgnoreBankFeedLineCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public IgnoreBankFeedLineCommandHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task Handle(IgnoreBankFeedLineCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var line = await _db.BankFeedLines
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy dòng ngân hàng.");

        if (string.Equals(line.Status, BankFeedLineStatuses.Matched, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictAppException("Dòng ngân hàng đã khớp — không thể bỏ qua.");
        }

        if (string.Equals(line.Status, BankFeedLineStatuses.Ignored, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        line.Status = BankFeedLineStatuses.Ignored;
        line.IgnoredAt = DateTimeOffset.UtcNow;
        line.IgnoreReason = string.IsNullOrWhiteSpace(request.IgnoreReason)
            ? null
            : request.IgnoreReason.Trim();
        await _db.SaveChangesAsync(cancellationToken);
    }
}
