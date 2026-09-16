import type { ReactNode } from "react";

/** Bottom analytics row — 1–3 chart panels. */
export function AnalyticsRow({
  children,
  columns = 3,
}: {
  children: ReactNode;
  columns?: 1 | 2 | 3;
}) {
  return (
    <div
      className={`analytics-row analytics-row-${columns}`}
      style={{ marginTop: "1rem" }}
    >
      {children}
    </div>
  );
}

export function AnalyticsPanel({
  title,
  children,
}: {
  title?: ReactNode;
  children: ReactNode;
}) {
  return (
    <div className="panel analytics-panel">
      {title ? <h3 className="analytics-panel-title">{title}</h3> : null}
      {children}
    </div>
  );
}
