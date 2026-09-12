using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;

namespace LCMS.Application.BankFeed.Commands;

public sealed record CreateBankFeedLineCommand(
    DateOnly ValueDate,
    decimal Amount,
    string CurrencyCode,
    string? Direction,
    string? BankReference,
    string? CounterpartyName,
    string? Description) : IRequest<Guid>;

public sealed class CreateBankFeedLineCommandValidator : AbstractValidator<CreateBankFeedLineCommand>
{
    public CreateBankFeedLineCommandValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Số tiền dòng ngân hàng phải lớn hơn 0.");
        RuleFor(x => x.CurrencyCode)
            .NotEmpty().WithMessage("Mã tiền tệ không được để trống.")
            .Length(3).WithMessage("Mã tiền tệ phải gồm 3 ký tự.");
        RuleFor(x => x.Direction)
            .Must(d => d is null
                || string.Equals(d.Trim(), BankFeedDirections.Credit, StringComparison.OrdinalIgnoreCase)
                || string.Equals(d.Trim(), BankFeedDirections.Debit, StringComparison.OrdinalIgnoreCase))
            .WithMessage("Chiều dòng ngân hàng phải là credit (thu) hoặc debit (chi).");
        RuleFor(x => x.BankReference).MaximumLength(128).When(x => x.BankReference is not null);
        RuleFor(x => x.CounterpartyName).MaximumLength(256).When(x => x.CounterpartyName is not null);
        RuleFor(x => x.Description).MaximumLength(2048).When(x => x.Description is not null);
    }
}

public sealed class CreateBankFeedLineCommandHandler : IRequestHandler<CreateBankFeedLineCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public CreateBankFeedLineCommandHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<Guid> Handle(CreateBankFeedLineCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var direction = string.IsNullOrWhiteSpace(request.Direction)
            ? BankFeedDirections.Credit
            : request.Direction.Trim().ToLowerInvariant();

        var line = new BankFeedLine
        {
            TenantId = _tenantContext.TenantId!.Value,
            ValueDate = request.ValueDate,
            Amount = decimal.Round(request.Amount, 4, MidpointRounding.AwayFromZero),
            CurrencyCode = request.CurrencyCode.Trim().ToUpperInvariant(),
            Direction = direction,
            BankReference = string.IsNullOrWhiteSpace(request.BankReference) ? null : request.BankReference.Trim(),
            CounterpartyName = string.IsNullOrWhiteSpace(request.CounterpartyName) ? null : request.CounterpartyName.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Status = BankFeedLineStatuses.Unmatched
        };

        _db.BankFeedLines.Add(line);
        await _db.SaveChangesAsync(cancellationToken);
        return line.Id;
    }
}
