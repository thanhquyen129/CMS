"use client";

import Link from "next/link";

/** Designer topbar utilities — no fake notification counts. */
export function TopbarAccount({
  displayName,
  roleLabel = "Người dùng",
}: {
  displayName: string;
  roleLabel?: string;
}) {
  const name = displayName.trim() || "Người dùng";
  const initials = name
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((p) => p[0]?.toUpperCase() ?? "")
    .join("") || "U";

  return (
    <div className="topbar-account">
      <Link
        className="topbar-icon-btn"
        href="/queues/exceptions"
        title="Hàng đợi ngoại lệ"
        aria-label="Hàng đợi ngoại lệ"
      >
        <span className="topbar-bell" aria-hidden="true" />
      </Link>
      <Link
        className="topbar-icon-btn"
        href="/workflow"
        title="Trợ giúp / bản đồ luồng"
        aria-label="Trợ giúp"
      >
        <span className="topbar-help" aria-hidden="true">?</span>
      </Link>
      <span className="topbar-lang" title="Ngôn ngữ giao diện (khóa CP6.5)">
        <span className="topbar-flag" aria-hidden="true">
          VI
        </span>
      </span>
      <div className="topbar-user" title={name}>
        <span className="topbar-avatar" aria-hidden="true">
          {initials}
        </span>
        <span className="topbar-user-text">
          <strong>{name}</strong>
          <small>{roleLabel}</small>
        </span>
      </div>
    </div>
  );
}
