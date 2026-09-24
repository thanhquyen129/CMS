"use client";

import type { FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useId, useState, useTransition } from "react";
import { term, type TerminologyMap } from "@/lib/terminology";
import { newIdempotencyKey, withIdempotency, withRowVersion } from "@/lib/idempotency";

type Props = {
  terms: TerminologyMap;
  closeId: string;
  canRun: boolean;
  rowVersion?: string | null;
};

export function ReopenCloseButton({ terms, closeId, canRun, rowVersion }: Props) {
  const router = useRouter();
  const dialogTitleId = useId();
  const [open, setOpen] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [isPending, startTransition] = useTransition();

  const reopenLabel = term(
    terms,
    "REOPEN_FINANCIAL_CLOSE",
    "Mở lại chốt tài chính"
  );
  const snapshotLabel = term(
    terms,
    "FINANCIAL_CLOSE_SNAPSHOT",
    "Bản chốt tài chính"
  );

  if (!canRun) return null;

  async function onConfirm(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setSubmitting(true);
    setError(null);
    const fd = new FormData(e.currentTarget);
    const reason = String(fd.get("reason") ?? "").trim();
    if (!reason) {
      setError("Cần lý do mở lại.");
      setSubmitting(false);
      return;
    }

    try {
      const res = await fetch(`/bff/financial-closes/${closeId}/reopen`, {
        method: "POST",
        headers: withRowVersion(
          withIdempotency(
            { "Content-Type": "application/json" },
            newIdempotencyKey("close-reopen")
          ),
          rowVersion
        ),
        body: JSON.stringify({ reason }),
      });

      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }

      if (!res.ok && res.status !== 204) {
        const body = (await res.json().catch(() => ({}))) as {
          message?: string;
        };
        setError(body.message || "Mở lại thất bại.");
        return;
      }

      setOpen(false);
      startTransition(() => router.refresh());
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <>
      <button
        type="button"
        className="btn btn-ghost"
        onClick={() => {
          setError(null);
          setOpen(true);
        }}
        disabled={isPending}
      >
        {reopenLabel}
      </button>

      {error && !open ? (
        <div
          className="alert alert-error"
          role="alert"
          style={{ marginTop: "0.75rem" }}
        >
          {error}
        </div>
      ) : null}

      {open ? (
        <div className="dialog-backdrop" role="presentation">
          <form
            className="dialog"
            role="dialog"
            aria-modal="true"
            aria-labelledby={dialogTitleId}
            onSubmit={onConfirm}
          >
            <h2 id={dialogTitleId}>{reopenLabel}?</h2>
            <p>
              Snapshot cũ <strong>không</strong> bị sửa. Chỉ mở lại phiên để
              chỉnh nghiệp vụ rồi tạo {snapshotLabel.toLowerCase()} mới.
            </p>
            {error ? (
              <div className="alert alert-error" role="alert">
                {error}
              </div>
            ) : null}
            <div className="field">
              <label htmlFor="reason">Lý do</label>
              <input
                id="reason"
                name="reason"
                required
                maxLength={1024}
                disabled={submitting}
              />
            </div>
            <div className="dialog-actions">
              <button
                type="button"
                className="btn btn-ghost"
                onClick={() => !submitting && setOpen(false)}
                disabled={submitting}
              >
                Hủy
              </button>
              <button type="submit" className="btn" disabled={submitting}>
                {submitting ? "Đang mở lại…" : "Xác nhận mở lại"}
              </button>
            </div>
          </form>
        </div>
      ) : null}
    </>
  );
}
