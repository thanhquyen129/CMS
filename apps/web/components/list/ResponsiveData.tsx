import type { ReactNode } from "react";

type Props = {
  /** Full table for wide content areas. */
  table: ReactNode;
  /** Compact transaction list for drawers, side panels and narrow viewports. */
  list: ReactNode;
  /** "list" forces the compact list (e.g. inside a drawer); "auto" switches on container width. */
  layout?: "auto" | "list";
};

/**
 * UI-TABLE-01: one data presentation pattern for LCMS histories.
 * Wide container → table; container ≤ 44rem (drawer, side panel, mobile) → compact list. No forced horizontal scroll.
 */
export function ResponsiveData({ table, list, layout = "auto" }: Props) {
  return (
    <div className={layout === "list" ? "rdata is-list" : "rdata"}>
      {layout === "list" ? null : <div className="rdata-table">{table}</div>}
      <div className="rdata-list">{list}</div>
    </div>
  );
}
