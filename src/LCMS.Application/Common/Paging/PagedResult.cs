namespace LCMS.Application.Common.Paging;

/// <summary>Standard list page envelope (1-based page index).</summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount);
