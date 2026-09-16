namespace LCMS.Application.Common.Paging;

public static class PagingNormalize
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    /// <summary>
    /// When both page and pageSize are null, return the full list as a single page
    /// (KPI / legacy callers). Otherwise clamp to 1-based page and [1, MaxPageSize].
    /// </summary>
    public static (int Page, int PageSize, bool ApplyPaging) Normalize(int? page, int? pageSize)
    {
        if (page is null && pageSize is null)
        {
            return (1, 0, false);
        }

        var p = page is null or < 1 ? 1 : page.Value;
        var ps = pageSize is null or < 1 ? DefaultPageSize : pageSize.Value;
        if (ps > MaxPageSize)
        {
            ps = MaxPageSize;
        }

        return (p, ps, true);
    }

    public static PagedResult<T> Empty<T>(int page, int pageSize, bool applyPaging) =>
        new([], applyPaging ? page : 1, applyPaging ? pageSize : 0, 0);
}
