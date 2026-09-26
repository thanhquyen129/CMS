"use client";

import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";

export function SyncVcbFxButton() {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [info, setInfo] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [pending, startTransition] = useTransition();

  async function onClick() {
    setError(null);
    setInfo(null);
    setBusy(true);
    try {
      const res = await fetch("/bff/admin/fx-rates/sync-vcb", {
        method: "POST",
        headers: { Accept: "application/json" },
      });
      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }
      const body = (await res.json().catch(() => ({}))) as {
        message?: string;
        upserted?: number;
        rateDate?: string;
        note?: string;
        errors?: Record<string, string[]>;
      };
      if (!res.ok) {
        const detail =
          body.message ||
          (body.errors ? Object.values(body.errors).flat().join(" ") : null) ||
          "Đồng bộ tỷ giá VCB thất bại.";
        setError(detail);
        return;
      }
      setInfo(
        `Đã cập nhật ${body.upserted ?? 0} tỷ giá (${body.rateDate ?? "hôm nay"}). ${body.note ?? ""}`
      );
      startTransition(() => router.refresh());
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      setBusy(false);
    }
  }

  const loading = busy || pending;

  return (
    <div className="stack" style={{ gap: "0.5rem" }}>
      <button
        type="button"
        className="btn"
        onClick={onClick}
        disabled={loading}
      >
        {loading ? "Đang lấy tỷ giá VCB…" : "Cập nhật tỷ giá Vietcombank"}
      </button>
      {info ? (
        <p className="note" role="status">
          {info}
        </p>
      ) : null}
      {error ? (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      ) : null}
    </div>
  );
}
