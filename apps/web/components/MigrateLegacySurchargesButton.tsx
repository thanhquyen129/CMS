"use client";

import { useState } from "react";

export function MigrateLegacySurchargesButton() {
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function run() {
    setBusy(true);
    setError(null);
    setMessage(null);
    try {
      const res = await fetch("/bff/surcharges/migrate-legacy", { method: "POST", headers: { Accept: "application/json" } });
      const payload = (await res.json().catch(() => ({}))) as {
        message?: string;
        migrated?: number;
        skippedBase?: number;
        skippedUnknown?: number;
        skippedPublished?: number;
        alreadyMapped?: number;
      };
      if (!res.ok) {
        setError(payload.message || "Không chuyển được phụ phí cũ.");
        return;
      }
      setMessage(
        `Đã sao chép ${payload.migrated ?? 0} phụ phí nháp. Giữ nguyên cước chính ${payload.skippedBase ?? 0}, chưa phân loại ${payload.skippedUnknown ?? 0}, phiên bản đã phát hành ${payload.skippedPublished ?? 0}, đã có mapping ${payload.alreadyMapped ?? 0}.`
      );
    } catch {
      setError("Không kết nối được máy chủ.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div>
      <button className="btn" type="button" disabled={busy} onClick={run}>
        {busy ? "Đang chuyển…" : "Chuyển phụ phí nháp cũ"}
      </button>
      {message ? <p className="notice">{message}</p> : null}
      {error ? <div className="alert alert-error" role="alert">{error}</div> : null}
    </div>
  );
}
