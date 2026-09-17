"use client";

import type { ReactNode } from "react";
import { useEffect } from "react";

type DetailDrawerProps = {
  open: boolean;
  title: ReactNode;
  subtitle?: ReactNode;
  onClose: () => void;
  children: ReactNode;
  footer?: ReactNode;
  /** Accessible name for the dialog. */
  labelledById?: string;
};

/** Right-side master-detail panel (PO UI baseline). */
export function DetailDrawer({
  open,
  title,
  subtitle,
  onClose,
  children,
  footer,
  labelledById = "detail-drawer-title",
  wide = false,
}: DetailDrawerProps & { wide?: boolean }) {
  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") onClose();
    };
    document.addEventListener("keydown", onKey);
    const prev = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    return () => {
      document.removeEventListener("keydown", onKey);
      document.body.style.overflow = prev;
    };
  }, [open, onClose]);

  if (!open) return null;

  return (
    <div className="detail-drawer-root">
      <button
        type="button"
        className="detail-drawer-backdrop"
        aria-label="Đóng panel chi tiết"
        onClick={onClose}
      />
      <aside
        className={wide ? "detail-drawer detail-drawer-wide" : "detail-drawer"}
        role="dialog"
        aria-modal="true"
        aria-labelledby={labelledById}
      >
        <header className="detail-drawer-header">
          <div className="detail-drawer-heading">
            <h2 id={labelledById} className="detail-drawer-title">
              {title}
            </h2>
            {subtitle ? (
              <div className="detail-drawer-subtitle">{subtitle}</div>
            ) : null}
          </div>
          <button
            type="button"
            className="btn btn-ghost btn-sm"
            onClick={onClose}
            aria-label="Đóng"
          >
            ✕
          </button>
        </header>
        <div className="detail-drawer-body">{children}</div>
        {footer ? <footer className="detail-drawer-footer">{footer}</footer> : null}
      </aside>
    </div>
  );
}
