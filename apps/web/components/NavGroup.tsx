"use client";

import type { ReactNode } from "react";
import { usePathname } from "next/navigation";
import { useState } from "react";
import { isNavGroupActive } from "@/lib/nav-match";
import { NavIcon, type NavIconName } from "./NavIcon";

type NavGroupProps = {
  label: string;
  icon: NavIconName;
  openByDefault?: boolean;
  active?: boolean;
  match?: string[];
  children: ReactNode;
};

/** Expandable sidebar group — PO module with child screens. */
export function NavGroup({
  label,
  icon,
  openByDefault = false,
  active = false,
  match,
  children,
}: NavGroupProps) {
  const pathname = usePathname();
  const routeActive = match ? isNavGroupActive(match, pathname) : active;
  const [userOpen, setUserOpen] = useState<boolean | null>(null);
  const open = userOpen ?? (openByDefault || routeActive);

  return (
    <div
      className={`nav-group${open ? " is-open" : ""}${routeActive ? " is-active" : ""}`}
    >
      <button
        type="button"
        className={`nav-item nav-group-toggle${routeActive && !open ? " is-current" : ""}`}
        aria-expanded={open}
        onClick={() => setUserOpen(!(userOpen ?? open))}
      >
        <NavIcon name={icon} />
        <span className="nav-item-label">{label}</span>
        <span className="nav-group-caret" aria-hidden="true" />
      </button>
      <div className="nav-group-children">{children}</div>
    </div>
  );
}
