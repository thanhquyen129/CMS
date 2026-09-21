"use client";

import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import type { InAppNotification } from "@/lib/tenant-admin-model";
import { formatDateTimeVi } from "@/lib/money";

export function NotificationInbox({ items }: { items: InAppNotification[] }) {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [isPending, startTransition] = useTransition();

  async function mark(id?: string) {
    setError(null);
    setBusy(true);
    try {
      const path = id
        ? `/bff/notifications/inbox/${id}/read`
        : "/bff/notifications/inbox/read-all";
      const res = await fetch(path, { method: "POST", headers: { Accept: "application/json" } });
      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }
      if (!res.ok) {
        const payload = (await res.json().catch(() => ({}))) as { message?: string };
        setError(payload.message || "Không đánh dấu đã đọc.");
        return;
      }
      startTransition(() => router.refresh());
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      setBusy(false);
    }
  }

  const waiting = busy || isPending;

  return (
    <div id="inbox">
      {error ? (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      ) : null}
      <div className="cta-row">
        <button
          type="button"
          className="btn btn-ghost"
          disabled={waiting || items.every((i) => i.isRead)}
          onClick={() => void mark()}
        >
          Đánh dấu tất cả đã đọc
        </button>
      </div>
      {items.length === 0 ? (
        <div className="empty-state" role="status">
          Không có thông báo.
        </div>
      ) : (
        <ul className="stack-list">
          {items.map((n) => (
            <li key={n.id} className={n.isRead ? "muted" : undefined}>
              <strong>{n.title}</strong>
              <p>{n.body}</p>
              <p className="muted small">
                {n.eventType} · {formatDateTimeVi(n.createdAt)}
                {n.isRead ? " · Đã đọc" : " · Chưa đọc"}
              </p>
              {n.href ? (
                <p>
                  <a href={n.href}>Mở liên kết</a>
                </p>
              ) : null}
              {!n.isRead ? (
                <button
                  type="button"
                  className="btn btn-ghost"
                  disabled={waiting}
                  onClick={() => void mark(n.id)}
                >
                  Đánh dấu đã đọc
                </button>
              ) : null}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
