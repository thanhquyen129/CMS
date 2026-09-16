"use client";

import type { ReactNode } from "react";
import { useState } from "react";

type NavGroupProps = {
  label: string;
  openByDefault?: boolean;
  children: ReactNode;
};

/** Expandable sidebar group — PO module with child screens. */
export function NavGroup({ label, openByDefault = false, children }: NavGroupProps) {
  const [open, setOpen] = useState(openByDefault);

  return (
    <div className={`nav-group${open ? " is-open" : ""}`}>
      <button
        type="button"
        className="nav-group-toggle"
        aria-expanded={open}
        onClick={() => setOpen((v) => !v)}
      >
        <span>{label}</span>
        <span className="nav-group-caret" aria-hidden="true" />
      </button>
      {open ? <div className="nav-group-children">{children}</div> : null}
    </div>
  );
}
