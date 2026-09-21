/**
 * Whether a sidebar href should look current for this pathname + query.
 * List routes stay active on detail URLs; `/new` is a sibling, not a child.
 */
export function isNavHrefActive(
  href: string,
  pathname: string,
  search: string
): boolean {
  const url = new URL(href, "http://local.invalid");
  const target = url.pathname;
  const current = new URLSearchParams(
    search.startsWith("?") ? search.slice(1) : search
  );

  if (target === "/ap-ar") {
    const want = url.searchParams.get("tab");
    const tab = current.get("tab");
    if (want === "ar") {
      return pathname.startsWith("/ap-ar") && tab === "ar";
    }
    if (want === "ap") {
      return pathname.startsWith("/ap-ar") && tab !== "ar";
    }
  }

  for (const [key, value] of url.searchParams.entries()) {
    if ((current.get(key) ?? "") !== value) return false;
  }

  if (pathname === target) return true;
  if (!pathname.startsWith(`${target}/`)) return false;
  const rest = pathname.slice(target.length);
  if (rest === "/new" || rest.startsWith("/new/")) return false;
  return true;
}

/** True when the current path belongs to a sidebar group. */
export function isNavGroupActive(prefixes: string[], pathname: string): boolean {
  return prefixes.some((prefix) => pathname === prefix || pathname.startsWith(`${prefix}/`));
}
