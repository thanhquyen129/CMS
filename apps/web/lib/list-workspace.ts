/** Shared helpers for UI-02 list workspaces (Bill / Order). */

export function isoInRange(
  iso: string | null | undefined,
  from?: string,
  to?: string
): boolean {
  if (!from && !to) return true;
  if (!iso) return false;
  const t = Date.parse(iso);
  if (Number.isNaN(t)) return false;
  if (from) {
    const start = Date.parse(`${from}T00:00:00`);
    if (!Number.isNaN(start) && t < start) return false;
  }
  if (to) {
    const end = Date.parse(`${to}T23:59:59.999`);
    if (!Number.isNaN(end) && t > end) return false;
  }
  return true;
}

/** Whole days from now until iso; negative = overdue. */
export function daysUntil(iso: string): number {
  const target = Date.parse(iso);
  if (Number.isNaN(target)) return Number.POSITIVE_INFINITY;
  const now = Date.now();
  return Math.ceil((target - now) / 86_400_000);
}

export function remainingLabel(days: number): { text: string; tone: "ok" | "warn" | "bad" } {
  if (days < 0) return { text: `Quá hạn ${Math.abs(days)} ngày`, tone: "bad" };
  if (days === 0) return { text: "Hôm nay", tone: "bad" };
  if (days === 1) return { text: "1 ngày", tone: "bad" };
  if (days <= 3) return { text: `${days} ngày`, tone: "warn" };
  return { text: `${days} ngày`, tone: "ok" };
}

export function uniqueSorted(values: Array<string | null | undefined>): string[] {
  return Array.from(
    new Set(values.map((v) => v?.trim()).filter((v): v is string => Boolean(v)))
  ).sort((a, b) => a.localeCompare(b, "vi"));
}

export function textMatches(
  haystacks: Array<string | null | undefined>,
  q?: string
): boolean {
  const needle = q?.trim().toLowerCase();
  if (!needle) return true;
  return haystacks.some((h) => (h ?? "").toLowerCase().includes(needle));
}

export type DeadlineRow = {
  id: string;
  code: string;
  party: string;
  item: string;
  days: number;
};

export function upcomingDeadlines<T extends { id: string; etdAt?: string | null }>(
  items: T[],
  label: (row: T) => { code: string; party: string; item?: string },
  horizonDays = 14,
  limit = 5
): DeadlineRow[] {
  return items
    .filter((x) => x.etdAt)
    .map((x) => {
      const meta = label(x);
      return {
        id: x.id,
        code: meta.code,
        party: meta.party,
        item: meta.item ?? "ETD",
        days: daysUntil(x.etdAt!),
      };
    })
    .filter((x) => x.days <= horizonDays)
    .sort((a, b) => a.days - b.days)
    .slice(0, limit);
}

export type RankRow = {
  name: string;
  amount: number;
  currency: string;
  pct: number;
};

export function topByAmount(
  groups: Array<{ name: string; amount: number; currency: string }>,
  limit = 5
): RankRow[] {
  const ranked = groups
    .filter((g) => g.name && g.amount > 0)
    .sort((a, b) => b.amount - a.amount)
    .slice(0, limit);
  const max = ranked[0]?.amount ?? 0;
  return ranked.map((g) => ({
    ...g,
    pct: max > 0 ? Math.round((g.amount / max) * 100) : 0,
  }));
}

export function downloadCsv(
  filename: string,
  headers: string[],
  rows: Array<Array<string | number | null | undefined>>
): void {
  const esc = (v: string | number | null | undefined) =>
    `"${String(v ?? "").replace(/"/g, '""')}"`;
  const csv = [headers.map(esc).join(","), ...rows.map((r) => r.map(esc).join(","))].join("\n");
  const blob = new Blob(["\uFEFF" + csv], { type: "text/csv;charset=utf-8" });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = filename;
  a.click();
  URL.revokeObjectURL(url);
}
