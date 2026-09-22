"use client";

import { useRouter } from "next/navigation";
import { useCallback, useId, useState, useTransition } from "react";
import { term, type TerminologyMap } from "@/lib/terminology";
import { newIdempotencyKey, withIdempotency } from "@/lib/idempotency";
import { formatApiErrorMessage, readApiErrorBody } from "@/lib/api-error";

type Props = {
  terms: TerminologyMap;
  closeId: string;
  /** Status allows snapshot (Open / Reopened). */
  canRun: boolean;
  /** Server eligibility — all gates passed. */
  eligible: boolean;
  /** Optional short reason when blocked (VI). */
  blockedHint?: string | null;
};

export function CloseSnapshotButton({
  terms,
  closeId,
  canRun,
  eligible,
  blockedHint,
}: Props) {
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
    if (!eligible) return;
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
        const body = await readApiErrorBody(res);
        setError(
          formatApiErrorMessage(
            body,
            res.status === 409
              ? "Không tạo bản chốt (eligibility / trạng thái). Kiểm tra và thử lại."
              : "Tạo bản chốt thất bại."
          )
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
  }, [closeId, eligible, router]);

  if (!canRun) return null;

  const busy = submitting || isPending;
  const blockReason =
    blockedHint?.trim() ||
    "Còn điều kiện chặn — xử lý checklist bên trên rồi mới tạo bản chốt.";

  return (
    <>
      <button
        type="button"
        className="btn"
        onClick={() => {
          if (!eligible) return;
          setError(null);
          setOpen(true);
        }}
        disabled={busy || !eligible}
        title={!eligible ? blockReason : undefined}
        aria-disabled={!eligible}
      >
        Tạo {snapshotLabel.toLowerCase()}
      </button>

      {!eligible ? (
        <p className="note" role="status" style={{ marginTop: "0.5rem" }}>
          {blockReason}
        </p>
      ) : null}

      {error && !open ? (
        <div
          className="alert alert-error"
          role="alert"
          style={{ marginTop: "0.75rem" }}
        >
          {error}
        </div>
      ) : null}

      {open && eligible ? (
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
