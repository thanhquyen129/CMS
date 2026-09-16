import type { ReactNode } from "react";

/** Table caption + optional toolbar (export) wrapping children table. */
export function DataTableShell({
  title,
  count,
  toolbar,
  children,
}: {
  title: string;
  count?: number;
  toolbar?: ReactNode;
  children: ReactNode;
}) {
  return (
    <div className="data-table-shell">
      <div className="data-table-shell-head">
        <h2 className="data-table-shell-title">
          {title}
          {count != null ? (
            <span className="muted"> ({count})</span>
          ) : null}
        </h2>
        {toolbar ? <div className="data-table-shell-toolbar">{toolbar}</div> : null}
      </div>
      <div className="table-wrap">{children}</div>
    </div>
  );
}
