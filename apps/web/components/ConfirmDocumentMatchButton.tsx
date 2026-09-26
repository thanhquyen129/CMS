"use client";

import { useRouter } from "next/navigation";
import { useCallback, useId, useState, useTransition } from "react";
import { term, type TerminologyMap } from "@/lib/terminology";
import { newIdempotencyKey, withIdempotency, withRowVersion } from "@/lib/idempotency";

type Props = {
  terms: TerminologyMap;
  matchId: string;
  canConfirm: boolean;
  rowVersion?: string | null;
};

export function ConfirmDocumentMatchButton({
  terms,
  matchId,
  canConfirm,
  rowVersion,
}: Props) {
  const router = useRouter();
  const dialogTitleId = useId();
  const [open, setOpen] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [isPending, startTransition] = useTransition();

  const confirmLabel = term(terms, "MATCH_CONFIRMED", "Xác nhận phiên khớp");

  const close = useCallback(() => {
    if (submitting) return;
    setOpen(false);
  }, [submitting]);

  const runConfirm = useCallback(async () => {
    setSubmitting(true);
    setError(null);
    try {
      const res = await fetch(`/bff/document-matches/${matchId}/confirm`, {
        method: "POST",
        headers: withRowVersion(withIdempotency({}, newIdempotencyKey("doc-match")), rowVersion),
      });

      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }

      if (!res.ok && res.status !== 204) {
        const body = (await res.json().catch(() => ({}))) as {
          message?: string;
        };
        setError(
          body.message ||
            (res.status === 409
              ? "Không thể xác nhận phiên khớp ở trạng thái hiện tại."
              : "Xác nhận phiên khớp thất bại.")
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
  }, [matchId, router, rowVersion]);

  if (!canConfirm) return null;

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
        {confirmLabel}
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
            <h2 id={dialogTitleId}>Xác nhận phiên khớp?</h2>
            <p>
              Sau khi xác nhận, không thêm chi tiết mới. Có thể hủy chi tiết rồi
              hủy phiên nếu cần. Không tạo chi phí/doanh thu.
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
                Đóng
              </button>
              <button
                type="button"
                className="btn"
                onClick={runConfirm}
                disabled={submitting}
              >
                {submitting ? "Đang xác nhận…" : "Xác nhận phiên"}
              </button>
            </div>
          </div>
        </div>
      ) : null}
    </>
  );
}
