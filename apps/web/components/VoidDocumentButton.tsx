"use client";

import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { formatApiErrorMessage } from "@/lib/api-error";

type Props = {
  documentId: string;
};

export function VoidDocumentButton({ documentId }: Props) {
  const router = useRouter();
  const [open, setOpen] = useState(false);
  const [reason, setReason] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [isPending, startTransition] = useTransition();

  async function submit() {
    const trimmed = reason.trim();
    if (!trimmed) {
      setError("Nhập lý do void.");
      return;
    }
    setBusy(true);
    setError(null);
    try {
      const res = await fetch(`/bff/financial-documents/${documentId}/cancel`, {
        method: "POST",
        headers: { "Content-Type": "application/json", Accept: "application/json" },
        body: JSON.stringify({ reason: trimmed, void: true }),
      });
      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }
      if (!res.ok && res.status !== 204) {
        const body = (await res.json().catch(() => ({}))) as {
          message?: string;
          errors?: Record<string, string[]>;
        };
        setError(formatApiErrorMessage(body, "Void chứng từ thất bại."));
        return;
      }
      setOpen(false);
      startTransition(() => router.refresh());
    } catch {
      setError("Không kết nối được máy chủ.");
    } finally {
      setBusy(false);
    }
  }

  if (!open) {
    return (
      <button
        type="button"
        className="btn btn-ghost"
        disabled={isPending}
        onClick={() => setOpen(true)}
      >
        Void chứng từ
      </button>
    );
  }

  return (
    <div className="stack-panels">
      <label htmlFor="void-reason">Lý do void</label>
      <input
        id="void-reason"
        value={reason}
        maxLength={512}
        disabled={busy}
        onChange={(e) => setReason(e.target.value)}
      />
      {error ? (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      ) : null}
      <div className="cta-row">
        <button type="button" className="btn" disabled={busy} onClick={() => void submit()}>
          {busy ? "Đang void…" : "Xác nhận void"}
        </button>
        <button
          type="button"
          className="btn btn-ghost"
          disabled={busy}
          onClick={() => setOpen(false)}
        >
          Đóng
        </button>
      </div>
      <p className="muted small">
        Void không xóa chứng từ. Nhận / chấp nhận / khớp giữ nguyên lịch sử.
      </p>
    </div>
  );
}
