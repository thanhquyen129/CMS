using LCMS.Application.Common.Exceptions;
using LCMS.Application.DocumentMatches.Queries;
using MediatR;

namespace LCMS.Application.DocumentMatches.Commands;

public sealed record ResolveDocumentMatchCommand(Guid MatchId, Guid SourceLineId) : IRequest<Guid>;

/// <summary>
/// Picks a single best candidate. Two candidates with the same delta are MATCH_AMBIGUOUS and nothing is saved.
/// </summary>
public sealed class ResolveDocumentMatchCommandHandler : IRequestHandler<ResolveDocumentMatchCommand, Guid>
{
    private readonly ISender _sender;

    public ResolveDocumentMatchCommandHandler(ISender sender) => _sender = sender;

    public async Task<Guid> Handle(ResolveDocumentMatchCommand request, CancellationToken cancellationToken)
    {
        var suggestions = await _sender.Send(new SuggestMatchCandidatesQuery(request.MatchId), cancellationToken);
        var forLine = suggestions.Where(s => s.SourceLineId == request.SourceLineId).ToList();
        if (forLine.Count == 0)
        {
            throw new ConflictAppException("Không có ứng viên khớp trong dung sai.");
        }

        var best = forLine.Min(s => s.AmountDelta);
        var ties = forLine.Where(s => s.AmountDelta == best).ToList();
        if (ties.Count > 1)
        {
            throw new ConflictAppException("MATCH_AMBIGUOUS: Nhiều ứng viên khớp cùng mức. Không tự chọn.");
        }

        var pick = ties[0];
        return await _sender.Send(
            new AddDocumentMatchDetailCommand(
                request.MatchId,
                request.SourceLineId,
                pick.TargetKind == "line" ? pick.TargetId : null,
                pick.TargetKind == "cost" ? pick.TargetId : null,
                pick.TargetKind == "revenue" ? pick.TargetId : null,
                pick.SuggestedMatchedAmount),
            cancellationToken);
    }
}
