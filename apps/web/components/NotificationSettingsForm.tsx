"use client";

import type { FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import type { NotificationSettings } from "@/lib/tenant-admin";

export function NotificationSettingsForm({ settings }: { settings: NotificationSettings }) {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [info, setInfo] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [isPending, startTransition] = useTransition();

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setError(null);
    setInfo(null);
    setBusy(true);
    const fd = new FormData(e.currentTarget);
    const events = settings.events.map((ev) => ({
      code: ev.code,
      name: ev.name,
      inApp: fd.get(`inApp:${ev.code}`) === "on",
      email: fd.get(`email:${ev.code}`) === "on",
    }));
    const body = {
      inAppEnabled: fd.get("inAppEnabled") === "on",
      emailEnabled: fd.get("emailEnabled") === "on",
      events,
    };
    try {
      const res = await fetch("/bff/notifications/settings", {
        method: "PUT",
        headers: { "Content-Type": "application/json", Accept: "application/json" },
        body: JSON.stringify(body),
      });
      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }
      if (!res.ok) {
        const payload = (await res.json().catch(() => ({}))) as { message?: string };
        setError(payload.message || "Không lưu được cài đặt thông báo.");
        return;
      }
      setInfo("Đã lưu. Email chỉ gửi khi SMTP đã cấu hình trên máy chủ.");
      startTransition(() => router.refresh());
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      setBusy(false);
    }
  }

  const waiting = busy || isPending;

  return (
    <form className="receive-form" onSubmit={onSubmit}>
      {error ? (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      ) : null}
      {info ? (
        <div className="alert alert-success" role="status">
          {info}
        </div>
      ) : null}
      {!settings.smtpConfigured ? (
        <p className="note">
          SMTP chưa cấu hình trên máy chủ. Tick email vẫn lưu, nhưng thư được ghi outbox là
          “chưa gửi” — không giả trạng thái đã gửi.
        </p>
      ) : (
        <p className="note">SMTP đã cấu hình. Email sẽ vào hàng đợi outbox `notification.email`.</p>
      )}
      <div className="form-grid">
        <label>
          <input
            type="checkbox"
            name="inAppEnabled"
            defaultChecked={settings.inAppEnabled}
            disabled={waiting}
          />{" "}
          Bật thông báo trong hệ thống
        </label>
        <label>
          <input
            type="checkbox"
            name="emailEnabled"
            defaultChecked={settings.emailEnabled}
            disabled={waiting}
          />{" "}
          Bật kênh email (toàn thuê bao)
        </label>
      </div>
      <div className="table-wrap" style={{ marginTop: "1rem" }}>
        <table className="data-table">
          <thead>
            <tr>
              <th scope="col">Sự kiện</th>
              <th scope="col">Trong hệ thống</th>
              <th scope="col">Email</th>
            </tr>
          </thead>
          <tbody>
            {settings.events.map((ev) => (
              <tr key={ev.code}>
                <td>
                  {ev.name}
                  <div className="muted small">{ev.code}</div>
                </td>
                <td>
                  <input
                    type="checkbox"
                    name={`inApp:${ev.code}`}
                    defaultChecked={ev.inApp}
                    disabled={waiting}
                  />
                </td>
                <td>
                  <input
                    type="checkbox"
                    name={`email:${ev.code}`}
                    defaultChecked={ev.email}
                    disabled={waiting}
                  />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <div className="cta-row">
        <button type="submit" className="btn" disabled={waiting}>
          {waiting ? "Đang lưu…" : "Lưu cài đặt thông báo"}
        </button>
      </div>
    </form>
  );
}
