/** Format money for Vietnamese B2B UI (vi-VN). Never invent totals — callers pass API amounts. */
export function formatMoney(amount: number, currencyCode: string): string {
  try {
    return new Intl.NumberFormat("vi-VN", {
      style: "currency",
      currency: currencyCode || "VND",
      maximumFractionDigits: currencyCode === "VND" ? 0 : 2,
    }).format(amount);
  } catch {
    return `${amount.toLocaleString("vi-VN")} ${currencyCode}`;
  }
}

const VIETNAM_TZ = "Asia/Ho_Chi_Minh";

/** Instant display in Vietnam time. Does not rewrite the stored UTC value. */
export function formatDateTimeVi(iso: string | Date): string {
  const d = typeof iso === "string" ? new Date(iso) : iso;
  if (Number.isNaN(d.getTime())) return "—";
  const date = new Intl.DateTimeFormat("vi-VN", {
    timeZone: VIETNAM_TZ,
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
  }).format(d);
  const time = new Intl.DateTimeFormat("vi-VN", {
    timeZone: VIETNAM_TZ,
    hour: "2-digit",
    minute: "2-digit",
    hourCycle: "h23",
  }).format(d);
  return `${date} ${time}`;
}

/** Date only (dd/MM/yyyy) for instants, shown on the Vietnam calendar. */
export function formatDateVi(iso: string | Date | null | undefined): string {
  if (!iso) return "—";
  const d = typeof iso === "string" ? new Date(iso) : iso;
  if (Number.isNaN(d.getTime())) return "—";
  return new Intl.DateTimeFormat("vi-VN", {
    timeZone: VIETNAM_TZ,
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
  }).format(d);
}

/**
 * Effective-from/to are date-only. Use the calendar date in the stored value
 * so a timezone conversion cannot move the day.
 */
export function formatDateOnlyVi(iso: string | Date | null | undefined): string {
  if (!iso) return "—";
  if (typeof iso === "string") {
    const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(iso);
    if (match) return `${match[3]}/${match[2]}/${match[1]}`;
  }
  return formatDateVi(iso);
}
