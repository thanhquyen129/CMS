"use client";

import type { ReactNode } from "react";
import { useState } from "react";
import { NavIcon, type NavIconName } from "./NavIcon";

type NavGroupProps = {
  label: string;
  icon: NavIconName;
  openByDefault?: boolean;
  active?: boolean;
  children: ReactNode;
};

/** Expandable sidebar group — PO module with child screens. */
export function NavGroup({
  label,
  icon,
  openByDefault = false,
  active = false,
  children,
}: NavGroupProps) {
  const [open, setOpen] = useState(openByDefault);

  return (
    <div
      className={`nav-group${open ? " is-open" : ""}${active ? " is-active" : ""}`}
    >
      <button
        type="button"
        className={`nav-item nav-group-toggle${active && !open ? " is-current" : ""}`}
        aria-expanded={open}
        onClick={() => setOpen((v) => !v)}
      >
        <NavIcon name={icon} />
        <span className="nav-item-label">{label}</span>
        <span className="nav-group-caret" aria-hidden="true" />
      </button>
      {open ? <div className="nav-group-children">{children}</div> : null}
    </div>
  );
}
