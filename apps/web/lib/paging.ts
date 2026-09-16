/** Shared paged list envelope from API (camelCase JSON). */

export type PagedResult<T> = {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
};

export function unwrapPaged<T>(data: T[] | PagedResult<T>): PagedResult<T> {
  if (Array.isArray(data)) {
    return {
      items: data,
      page: 1,
      pageSize: data.length || 20,
      totalCount: data.length,
    };
  }
  return {
    items: data.items ?? [],
    page: data.page ?? 1,
    pageSize: data.pageSize ?? data.items?.length ?? 20,
    totalCount: data.totalCount ?? data.items?.length ?? 0,
  };
}

export function pagingQuery(page?: number, pageSize?: number): string {
  const p = new URLSearchParams();
  if (page != null && page > 0) p.set("page", String(page));
  if (pageSize != null && pageSize > 0) p.set("pageSize", String(pageSize));
  return p.toString();
}
