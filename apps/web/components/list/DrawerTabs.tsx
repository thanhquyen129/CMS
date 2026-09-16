"use client";

import type { ReactNode } from "react";

export type DrawerTab = {
  id: string;
  label: ReactNode;
  badge?: number | string;
};

/** Horizontal tab strip for DetailDrawer bodies. */
export function DrawerTabs({
  tabs,
  activeId,
  onChange,
}: {
  tabs: DrawerTab[];
  activeId: string;
  onChange: (id: string) => void;
}) {
  return (
    <div className="drawer-tabs filter-tabs" role="tablist">
      {tabs.map((t) => (
        <button
          key={t.id}
          type="button"
          role="tab"
          aria-selected={t.id === activeId}
          className={t.id === activeId ? "active" : undefined}
          onClick={() => onChange(t.id)}
        >
          {t.label}
          {t.badge != null ? (
            <span className="drawer-tab-badge"> {t.badge}</span>
          ) : null}
        </button>
      ))}
    </div>
  );
}
