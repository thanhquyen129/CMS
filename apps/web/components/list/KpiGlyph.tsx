/** Compact glyph for list KPI cards (PO UI-02). */

const GLYPH: Record<string, string> = {
  doc: "▤",
  revenue: "▰",
  confirmed: "▥",
  actual: "▣",
  order: "▤",
  draft: "✎",
  link: "▣",
  unlink: "☐",
};

export function KpiGlyph({ name }: { name: keyof typeof GLYPH }) {
  return <span aria-hidden="true">{GLYPH[name]}</span>;
}
