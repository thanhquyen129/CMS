"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";

export function SurchargeVersionActions({
  surchargeId,
  versionId,
  published,
}: {
  surchargeId: string;
  versionId: string;
  published: boolean;
}) {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function post(url: string) {
    setError(null);
    setBusy(true);
    try {
      const res = await fetch(url, { method: "POST", headers: { Accept: "application/json" } });
      const payload = (await res.json().catch(() => ({}))) as { message?: string };
      if (!res.ok) {
        setError(payload.message || "Không thực hiện được.");
        return;
      }
      router.refresh();
    } catch {
      setError("Không kết nối được máy chủ.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="cta-row">
      {published ? (
        <button className="btn btn-primary" type="button" disabled={busy} onClick={() => post(`/bff/surcharges/${surchargeId}/versions`)}>
          Tạo phiên bản mới
        </button>
      ) : (
        <button className="btn btn-primary" type="button" disabled={busy} onClick={() => post(`/bff/surcharges/${surchargeId}/versions/${versionId}/publish`)}>
          Phát hành
        </button>
      )}
      {error ? <div className="alert alert-error" role="alert">{error}</div> : null}
    </div>
  );
}
