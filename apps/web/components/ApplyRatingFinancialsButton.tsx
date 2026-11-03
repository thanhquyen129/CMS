"use client";

import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";

type Props = {
  ratingId: string;
  directionLabel: string;
};

export function ApplyRatingFinancialsButton({ ratingId, directionLabel }: Props) {
  const router = useRouter();
  const [pending, startTransition] = useTransition();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [preview, setPreview] = useState<string | null>(null);
  const [warnings, setWarnings] = useState<string[]>([]);

  async function previewApply() {
    setError(null);
    setBusy(true);
    try {
      const res = await fetch(`/bff/ratings/${ratingId}/preview-apply`, {
        method: "POST",
        headers: { "Content-Type": "application/json", Accept: "application/json" },
        body: "{}",
      });
      if (!res.ok) {
        const payload = (await res.json().catch(() => ({}))) as { message?: string };
        setError(payload.message || "Không xem trước được ảnh hưởng.");
        return;
      }
      const data = (await res.json()) as {
        deltaCost: number;
        deltaRevenue: number;
        deltaProfit: number;
        warningsVi?: string[];
        lines?: { action: string; componentCode: string }[];
      };
      const lineSummary = (data.lines ?? [])
        .map((l) => `${l.componentCode}: ${l.action}`)
        .join("; ");
      setWarnings(data.warningsVi ?? []);
      setPreview(
        `Δ Chi phí ${data.deltaCost.toLocaleString("vi-VN")} · Δ Doanh thu ${data.deltaRevenue.toLocaleString("vi-VN")} · Δ Lợi nhuận ${data.deltaProfit.toLocaleString("vi-VN")}` +
          (lineSummary ? ` — ${lineSummary}` : "")
      );
    } catch {
      setError("Không kết nối được máy chủ.");
    } finally {
      setBusy(false);
    }
  }

  async function apply() {
    if (
      !window.confirm(
        `Áp dụng phương án ${directionLabel} vào hồ sơ tài chính? Thao tác chỉ cập nhật lớp Dự kiến theo nghĩa vụ kinh tế (không ghi đè Thực tế/Đã xác nhận).`
      )
    ) {
      return;
    }
    setError(null);
    setBusy(true);
    try {
      const idem = crypto.randomUUID();
      const res = await fetch(`/bff/ratings/${ratingId}/apply-financials`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Accept: "application/json",
          "Idempotency-Key": idem,
        },
        body: JSON.stringify({ idempotencyKey: idem }),
      });
      if (!res.ok) {
        const payload = (await res.json().catch(() => ({}))) as { message?: string };
        setError(payload.message || "Áp dụng thất bại.");
        return;
      }
      startTransition(() => router.refresh());
    } catch {
      setError("Không kết nối được máy chủ.");
    } finally {
      setBusy(false);
    }
  }

  const disabled = busy || pending;

  return (
    <div className="stack" style={{ gap: "0.5rem" }}>
      <div style={{ display: "flex", flexWrap: "wrap", gap: "0.5rem" }}>
        <button type="button" className="btn btn-sm btn-ghost" disabled={disabled} onClick={previewApply}>
          Xem trước ảnh hưởng
        </button>
        <button type="button" className="btn btn-sm" disabled={disabled} onClick={apply}>
          Áp dụng vào hồ sơ tài chính
        </button>
      </div>
      {preview ? (
        <div className="panel" role="status" style={{ padding: "10px 12px", border: "1px solid #cbd5e1", borderRadius: 8 }}>
          <strong>C. Preview Apply · Delta</strong>
          <p className="note" style={{ margin: "6px 0 0" }}>
            {preview}
          </p>
        </div>
      ) : null}
      {warnings.length > 0 ? (
        <div className="alert alert-warning" role="alert">
          <strong>Cảnh báo Actual / Confirmed:</strong> {warnings.join(" ")}
        </div>
      ) : null}
      {error ? (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      ) : null}
    </div>
  );
}
