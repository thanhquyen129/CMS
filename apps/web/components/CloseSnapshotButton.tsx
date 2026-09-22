"use client";

import { useRouter } from "next/navigation";
import { useCallback, useId, useState, useTransition } from "react";
import { term, type TerminologyMap } from "@/lib/terminology";
import { newIdempotencyKey, withIdempotency } from "@/lib/idempotency";

type Props = {
  terms: TerminologyMap;
  closeId: string;
  canRun: boolean;
};

export function CloseSnapshotButton({ terms, closeId, canRun }: Props) {
  const router = useRouter();
  const dialogTitleId = useId();
  const [open, setOpen] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [isPending, startTransition] = useTransition();

  const snapshotLabel = term(
    terms,
    "FINANCIAL_CLOSE_SNAPSHOT",
    "Bản chốt tài chính"
  );
  const lockedLabel = term(terms, "CLOSE_LOCKED", "Đã khóa chốt");
  const periodLock = term(terms, "PERIOD_LOCK", "Khóa kỳ");

  const close = useCallback(() => {
    if (submitting) return;
    setOpen(false);
  }, [submitting]);

  const runSnapshot = useCallback(async () => {
    setSubmitting(true);
    setError(null);
    try {
      const res = await fetch(`/bff/financial-closes/${closeId}/snapshot`, {
        method: "POST",
        headers: withIdempotency({}, newIdempotencyKey("close-snap")),
      });

      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }

      if (!res.ok) {
        const body = (await res.json().catch(() => ({}))) as {
          message?: string;
        };
        setError(
          body.message ||
            (res.status === 409
              ? "Không tạo bản chốt (eligibility / trạng thái). Kiểm tra và thử lại."
              : "Tạo bản chốt thất bại.")
        );
        return;
      }

      setOpen(false);
      startTransition(() => router.refresh());
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      setSubmitting(false);
    }
  }, [closeId, router]);

  if (!canRun) return null;

  return (
    <>
      <button
        type="button"
        className="btn"
        onClick={() => {
          setError(null);
          setOpen(true);
        }}
        disabled={isPending}
      >
        Tạo {snapshotLabel.toLowerCase()}
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
        <div
          className="dialog-backdrop"
          role="presentation"
          onClick={(e) => {
            if (e.target === e.currentTarget) close();
          }}
        >
          <div
            className="dialog"
            role="dialog"
            aria-modal="true"
            aria-labelledby={dialogTitleId}
          >
            <h2 id={dialogTitleId}>Tạo {snapshotLabel.toLowerCase()}?</h2>
            <p>
              Snapshot bất biến; lần chốt chuyển <strong>{lockedLabel}</strong>.
              Có thể kích hoạt {periodLock.toLowerCase()} — chặn xác nhận chi
              phí / phân bổ tất toán trong kỳ.
            </p>
            {error ? (
              <div className="alert alert-error" role="alert">
                {error}
              </div>
            ) : null}
            <div className="dialog-actions">
              <button
                type="button"
                className="btn btn-ghost"
                onClick={close}
                disabled={submitting}
              >
                Hủy
              </button>
              <button
                type="button"
                className="btn"
                onClick={runSnapshot}
                disabled={submitting}
              >
                {submitting ? "Đang chốt…" : "Xác nhận tạo bản chốt"}
              </button>
            </div>
          </div>
        </div>
      ) : null}
    </>
  );
}
