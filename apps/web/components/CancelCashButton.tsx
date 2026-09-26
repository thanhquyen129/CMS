"use client";

import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { formatApiErrorMessage } from "@/lib/api-error";

type Props = {
  kind: "payment" | "collection";
  cashId: string;
};

export function CancelCashButton({ kind, cashId }: Props) {
  const router = useRouter();
  const [open, setOpen] = useState(false);
  const [reason, setReason] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [isPending, startTransition] = useTransition();
  const label = kind === "payment" ? "Hủy thanh toán" : "Hủy phiếu thu";

  async function submit() {
    const trimmed = reason.trim();
    if (!trimmed) {
      setError("Nhập lý do hủy.");
      return;
    }
    setBusy(true);
    setError(null);
    const path =
      kind === "payment"
        ? `/bff/payments/${cashId}/cancel`
        : `/bff/collections/${cashId}/cancel`;
    try {
      const res = await fetch(path, {
        method: "POST",
        headers: { "Content-Type": "application/json", Accept: "application/json" },
        body: JSON.stringify({ reason: trimmed }),
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
        setError(formatApiErrorMessage(body, "Không hủy được."));
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

  return (
    <div className="cta-row">
      <button type="button" className="btn btn-ghost" onClick={() => setOpen(true)}>
        {label}
      </button>
      {open ? (
        <form
          className="stack-form"
          onSubmit={(e) => {
            e.preventDefault();
            void submit();
          }}
        >
          <label>
            Lý do
            <textarea
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              maxLength={512}
              required
              rows={3}
            />
          </label>
          <p className="note">
            Phân bổ nháp sẽ được hủy cùng phiếu. Phân bổ đã chốt phải hủy phân bổ trước.
            Không xóa số tiền.
          </p>
          {error ? (
            <div className="alert alert-error" role="alert">
              {error}
            </div>
          ) : null}
          <div className="cta-row">
            <button type="submit" className="btn" disabled={busy || isPending}>
              {busy ? "Đang hủy…" : "Xác nhận hủy"}
            </button>
            <button
              type="button"
              className="btn btn-ghost"
              onClick={() => setOpen(false)}
              disabled={busy}
            >
              Đóng
            </button>
          </div>
        </form>
      ) : null}
    </div>
  );
}
