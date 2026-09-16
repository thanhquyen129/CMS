"use client";

import type { ReactNode } from "react";
import { usePathname } from "next/navigation";
import { useEffect, useId, useState } from "react";
import { GlobalSearch } from "./GlobalSearch";
import { LogoutButton } from "./LogoutButton";
import { SidebarCollapseButton } from "./SidebarCollapseButton";

type ShellChromeProps = {
  brand: ReactNode;
  nav: ReactNode;
  children: ReactNode;
  topbarRight?: ReactNode;
};

export function ShellChrome({ brand, nav, children, topbarRight }: ShellChromeProps) {
  const pathname = usePathname();
  const sidebarId = useId();
  const [navOpen, setNavOpen] = useState(false);

  useEffect(() => {
    setNavOpen(false);
  }, [pathname]);

  useEffect(() => {
    if (!navOpen) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") setNavOpen(false);
    };
    document.addEventListener("keydown", onKey);
    const prev = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    return () => {
      document.removeEventListener("keydown", onKey);
      document.body.style.overflow = prev;
    };
  }, [navOpen]);

  return (
    <div className={`shell${navOpen ? " nav-open" : ""}`}>
      <header className="mobile-bar">
        <button
          type="button"
          className="nav-toggle"
          aria-expanded={navOpen}
          aria-controls={sidebarId}
          onClick={() => setNavOpen((o) => !o)}
        >
          <span className="nav-toggle-bars" aria-hidden="true" />
          <span className="sr-only">{navOpen ? "Đóng menu" : "Mở menu"}</span>
        </button>
        <div className="mobile-bar-brand">
          <span className="brand-mark" aria-hidden="true" />
          <div>
            LCMS
            <small>Logistics Cost Management System</small>
          </div>
        </div>
        <div className="mobile-bar-actions">
          <LogoutButton />
        </div>
      </header>

      <aside className="sidebar" id={sidebarId}>
        <div className="brand sidebar-brand">{brand}</div>
        <div className="sidebar-nav-scroll">{nav}</div>
        <div className="sidebar-footer">
          <div className="sidebar-footer-visual" aria-hidden="true" />
          <p className="sidebar-tagline">
            <em>Kiểm soát chi phí</em>
            <em>Tối ưu lợi nhuận</em>
            <em>Phát triển bền vững</em>
          </p>
          <div className="sidebar-footer-meta">
            <div>
              <p className="sidebar-version">LCMS v1.0.0</p>
              <p className="sidebar-copy">© 2025. All rights reserved.</p>
            </div>
            <SidebarCollapseButton />
          </div>
          <div className="sidebar-logout">
            <LogoutButton />
          </div>
        </div>
      </aside>

      <div className="main">
        <div className="topbar">
          <GlobalSearch />
          <div className="topbar-actions">{topbarRight}</div>
        </div>
        {children}
      </div>

      <button
        type="button"
        className="nav-backdrop"
        aria-label="Đóng menu"
        tabIndex={navOpen ? 0 : -1}
        onClick={() => setNavOpen(false)}
      />
    </div>
  );
}
