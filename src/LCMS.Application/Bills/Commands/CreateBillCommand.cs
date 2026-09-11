using FluentValidation;
using LCMS.Domain.Common;
using MediatR;

namespace LCMS.Application.Bills.Commands;

/// <summary>Sample CQRS command — creates Bill financial anchor (TD1).</summary>
public sealed record CreateBillCommand(
    Guid TenantId,
    string BillNo,
    string BillType,
    string? SourceSystem,
    string? ExternalId) : IRequest<Guid>;

public sealed class CreateBillCommandValidator : AbstractValidator<CreateBillCommand>
{
    public CreateBillCommandValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty()
            .WithMessage("Thiếu mã thuê bao.");

        RuleFor(x => x.BillNo)
            .NotEmpty()
            .WithMessage("Số Bill không được để trống.")
            .MaximumLength(64)
            .WithMessage("Số Bill không được vượt quá 64 ký tự.");

        RuleFor(x => x.BillType)
            .NotEmpty()
            .WithMessage("Loại Bill không được để trống.")
            .MaximumLength(64)
            .WithMessage("Loại Bill không được vượt quá 64 ký tự.");
    }
}

/// <summary>
/// Handler placeholder — persistence wired when Bill repository/UoW lands.
/// Keeps CQRS + Vietnamese validation contract in place for API pipeline.
/// </summary>
public sealed class CreateBillCommandHandler : IRequestHandler<CreateBillCommand, Guid>
{
    public Task<Guid> Handle(CreateBillCommand request, CancellationToken cancellationToken)
    {
        // Persistence will use LcmsDbContext in a follow-up slice.
        return Task.FromResult(UuidV7.NewId());
    }
}
