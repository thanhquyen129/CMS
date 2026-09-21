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
  /** Empty-state copy when inline panel has no selection. */
  emptyHint?: string;
};

/** Right-side master-detail panel (PO UI baseline). Overlay drawer or inline column. */
export function DetailDrawer({
  open,
  title,
  subtitle,
  onClose,
  children,
  footer,
  labelledById = "detail-drawer-title",
  wide = false,
  inline = false,
  emptyHint = "Chọn một dòng trên danh sách để xem chi tiết.",
}: DetailDrawerProps & { wide?: boolean; inline?: boolean }) {
  useEffect(() => {
    if (!open || inline) return;
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
  }, [open, onClose, inline]);

  const heading = (
    <header className={inline ? "list-detail-head" : "detail-drawer-header"}>
      <div className={inline ? "list-detail-heading" : "detail-drawer-heading"}>
        <h2 id={labelledById} className={inline ? "list-detail-title" : "detail-drawer-title"}>
          {title}
        </h2>
        {subtitle ? (
          <div className={inline ? "list-detail-subtitle" : "detail-drawer-subtitle"}>
            {subtitle}
          </div>
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
  );

  if (inline) {
    return (
      <aside
        className="list-detail-panel"
        aria-labelledby={open ? labelledById : undefined}
      >
        {open ? (
          <>
            {heading}
            <div className="list-detail-body">{children}</div>
            {footer ? <footer className="list-detail-footer">{footer}</footer> : null}
          </>
        ) : (
          <p className="muted" role="status">
            {emptyHint}
          </p>
        )}
      </aside>
    );
  }

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
