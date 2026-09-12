using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Reconciliations.Commands;

public sealed record ReconciliationDetailLine(
    string SourceType,
    Guid SourceId,
    string? TargetType,
    Guid? TargetId,
    decimal SourceAmount,
    decimal TargetAmount,
    decimal MatchedAmount,
    string CurrencyCode,
    string? Notes);

public sealed record AddReconciliationDetailBatchCommand(
    Guid ReconciliationId,
    IReadOnlyList<ReconciliationDetailLine> Details) : IRequest<IReadOnlyList<Guid>>;

public sealed class AddReconciliationDetailBatchCommandValidator
    : AbstractValidator<AddReconciliationDetailBatchCommand>
{
    private static readonly HashSet<string> AllowedObjectTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ReconciliationObjectTypes.Payment,
        ReconciliationObjectTypes.Collection,
        ReconciliationObjectTypes.Cost,
        ReconciliationObjectTypes.Revenue,
        ReconciliationObjectTypes.Document,
        ReconciliationObjectTypes.AccountsPayable,
        ReconciliationObjectTypes.AccountsReceivable,
        ReconciliationObjectTypes.BankLine,
        ReconciliationObjectTypes.Other
    };

    public AddReconciliationDetailBatchCommandValidator()
    {
        RuleFor(x => x.ReconciliationId).NotEmpty().WithMessage("Phiên đối soát không hợp lệ.");
        RuleFor(x => x.Details)
            .NotEmpty().WithMessage("Danh sách chi tiết đối soát không được để trống.")
            .Must(d => d.Count <= 200).WithMessage("Mỗi lần gửi tối đa 200 chi tiết đối soát.");
        RuleForEach(x => x.Details).ChildRules(line =>
        {
            line.RuleFor(l => l.SourceType)
                .NotEmpty().WithMessage("Loại nguồn đối soát không được để trống.")
                .Must(t => AllowedObjectTypes.Contains(t.Trim()))
                .WithMessage("Loại nguồn đối soát không hợp lệ.");
            line.RuleFor(l => l.SourceId).NotEmpty().WithMessage("Đối tượng nguồn không hợp lệ.");
            line.RuleFor(l => l.TargetType)
                .Must(t => t is null || AllowedObjectTypes.Contains(t.Trim()))
                .WithMessage("Loại đích đối soát không hợp lệ.");
            line.RuleFor(l => l.SourceAmount)
                .GreaterThanOrEqualTo(0).WithMessage("Số tiền nguồn không được âm.");
            line.RuleFor(l => l.TargetAmount)
                .GreaterThanOrEqualTo(0).WithMessage("Số tiền đích không được âm.");
            line.RuleFor(l => l.MatchedAmount)
                .GreaterThanOrEqualTo(0).WithMessage("Số tiền khớp không được âm.");
            line.RuleFor(l => l.CurrencyCode)
                .NotEmpty().WithMessage("Mã tiền tệ không được để trống.")
                .Length(3).WithMessage("Mã tiền tệ phải gồm 3 ký tự.");
            line.RuleFor(l => l.Notes).MaximumLength(2048).When(l => l.Notes is not null);
            line.RuleFor(l => l)
                .Must(l => !(l.TargetId.HasValue ^ !string.IsNullOrWhiteSpace(l.TargetType)))
                .WithMessage("Đích đối soát phải có đủ loại và mã, hoặc cả hai để trống.");
        });
    }
}

/// <summary>
/// Multi-detail batch add — same Variance rules as single-line; never opens Exception.
/// </summary>
public sealed class AddReconciliationDetailBatchCommandHandler
    : IRequestHandler<AddReconciliationDetailBatchCommand, IReadOnlyList<Guid>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IReconciliationDetailWriter _writer;

    public AddReconciliationDetailBatchCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IReconciliationDetailWriter writer)
    {
        _db = db;
        _tenantContext = tenantContext;
        _writer = writer;
    }

    public async Task<IReadOnlyList<Guid>> Handle(
        AddReconciliationDetailBatchCommand request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var tenantId = _tenantContext.TenantId!.Value;
        var session = await _db.Reconciliations
            .FirstOrDefaultAsync(r => r.Id == request.ReconciliationId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy phiên đối soát.");

        var exceptionCountBefore = await _db.Exceptions.CountAsync(cancellationToken);
        var ids = new List<Guid>(request.Details.Count);

        foreach (var line in request.Details)
        {
            var id = await _writer.AddAsync(
                session,
                tenantId,
                line.SourceType,
                line.SourceId,
                line.TargetType,
                line.TargetId,
                line.SourceAmount,
                line.TargetAmount,
                line.MatchedAmount,
                line.CurrencyCode,
                line.Notes,
                cancellationToken);
            ids.Add(id);
        }

        var exceptionCountAfter = await _db.Exceptions.CountAsync(cancellationToken);
        if (exceptionCountAfter != exceptionCountBefore)
        {
            throw new ConflictAppException(
                "Chi tiết đối soát không được tự tạo Ngoại lệ — Chênh lệch ≠ Ngoại lệ.");
        }

        return ids;
    }
}
