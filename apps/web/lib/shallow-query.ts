export const LIST_SELECTED_EVENT = "lcms:list-selected";

export type ListSelectedDetail = {
  pathname: string;
  id: string | null;
};

/**
 * Writes a new URL without a Next.js RSC navigation (no loading UI, no scroll jump).
 * Used for list-row selection that only needs a shareable `?selected=` deep link.
 */
export function replaceSearchShallow(href: string): ListSelectedDetail {
  const url = new URL(href, window.location.origin);
  const next = `${url.pathname}${url.search}${url.hash}`;
  const prev = window.history.state;
  window.history.replaceState(
    prev && typeof prev === "object" ? { ...prev } : prev,
    "",
    next
  );
  const detail: ListSelectedDetail = {
    pathname: url.pathname,
    id: url.searchParams.get("selected"),
  };
  window.dispatchEvent(new CustomEvent(LIST_SELECTED_EVENT, { detail }));
  return detail;
}

/** Subscribe to shallow list-selection URL changes on one pathname. */
export function listenListSelected(
  pathname: string,
  onId: (id: string | null) => void
): () => void {
  const handler = (event: Event) => {
    const detail = (event as CustomEvent<ListSelectedDetail>).detail;
    if (!detail || detail.pathname !== pathname) return;
    onId(detail.id);
  };
  window.addEventListener(LIST_SELECTED_EVENT, handler);
  return () => window.removeEventListener(LIST_SELECTED_EVENT, handler);
}
