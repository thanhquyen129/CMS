/** Client-side list paging via URL query (API lists still return full arrays). */

export const DEFAULT_PAGE_SIZE = 20;
export const PAGE_SIZE_OPTIONS = [10, 20, 50, 100] as const;

export function parsePage(raw: string | undefined | null, totalPages: number): number {
  const n = Number.parseInt(String(raw ?? "1"), 10);
  if (!Number.isFinite(n) || n < 1) return 1;
  if (totalPages < 1) return 1;
  return Math.min(n, totalPages);
}

export function parsePageSize(raw: string | undefined | null): number {
  const n = Number.parseInt(String(raw ?? String(DEFAULT_PAGE_SIZE)), 10);
  if ((PAGE_SIZE_OPTIONS as readonly number[]).includes(n)) return n;
  return DEFAULT_PAGE_SIZE;
}

export function totalPages(totalCount: number, pageSize: number): number {
  if (totalCount <= 0) return 1;
  return Math.max(1, Math.ceil(totalCount / pageSize));
}

export function slicePage<T>(items: T[], page: number, pageSize: number): T[] {
  const start = (page - 1) * pageSize;
  return items.slice(start, start + pageSize);
}

/** Merge page/pageSize into an existing path+query, preserving other params. */
export function hrefWithPage(
  basePath: string,
  current: URLSearchParams | Record<string, string | undefined | null>,
  page: number,
  pageSize: number,
  pageKey = "page",
  pageSizeKey = "pageSize"
): string {
  const p =
    current instanceof URLSearchParams
      ? new URLSearchParams(current)
      : new URLSearchParams();
  if (!(current instanceof URLSearchParams)) {
    for (const [k, v] of Object.entries(current)) {
      if (v != null && v !== "") p.set(k, v);
    }
  }
  if (page <= 1) p.delete(pageKey);
  else p.set(pageKey, String(page));
  if (pageSize === DEFAULT_PAGE_SIZE) p.delete(pageSizeKey);
  else p.set(pageSizeKey, String(pageSize));
  const qs = p.toString();
  return qs ? `${basePath}?${qs}` : basePath;
}
