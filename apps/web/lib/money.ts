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

export function formatDateTimeVi(iso: string | Date): string {
  const d = typeof iso === "string" ? new Date(iso) : iso;
  if (Number.isNaN(d.getTime())) return "—";
  return d.toLocaleString("vi-VN", {
    dateStyle: "short",
    timeStyle: "short",
  });
}

/** Date only (dd/MM/yyyy) for list columns matching PO mockup. */
export function formatDateVi(iso: string | Date | null | undefined): string {
  if (!iso) return "—";
  const d = typeof iso === "string" ? new Date(iso) : iso;
  if (Number.isNaN(d.getTime())) return "—";
  return d.toLocaleDateString("vi-VN");
}
