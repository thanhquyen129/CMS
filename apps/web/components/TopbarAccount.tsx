"use client";

import Link from "next/link";

/** Designer topbar utilities — notification count is live inbox, never a fake badge. */
export function TopbarAccount({
  displayName,
  roleLabel = "Người dùng",
  unreadNotifications = 0,
}: {
  displayName: string;
  roleLabel?: string;
  unreadNotifications?: number;
}) {
  const name = displayName.trim() || "Người dùng";
  const initials = name
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((p) => p[0]?.toUpperCase() ?? "")
    .join("") || "U";
  const unread = Math.max(0, unreadNotifications);

  return (
    <div className="topbar-account">
      <Link
        className="topbar-icon-btn"
        href="/settings/notifications#inbox"
        title={unread > 0 ? `${unread} thông báo chưa đọc` : "Hộp thư thông báo"}
        aria-label={unread > 0 ? `${unread} thông báo chưa đọc` : "Hộp thư thông báo"}
      >
        <span className="topbar-bell" aria-hidden="true" />
        {unread > 0 ? (
          <span className="topbar-badge">{unread > 99 ? "99+" : unread}</span>
        ) : null}
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
