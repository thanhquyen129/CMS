"use client";

import { useRouter } from "next/navigation";
import { useCallback, useId, useState, useTransition } from "react";
import { term, type TerminologyMap } from "@/lib/terminology";

type Props = {
  terms: TerminologyMap;
  matchId: string;
  documentId: string;
  canCancel: boolean;
};

export function CancelDocumentMatchButton({
  terms,
  matchId,
  documentId,
  canCancel,
}: Props) {
  const router = useRouter();
  const dialogTitleId = useId();
  const [open, setOpen] = useState(false);
  const [reason, setReason] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [isPending, startTransition] = useTransition();

  const cancelLabel = term(terms, "MATCH_CANCELLED", "Hủy phiên khớp");

  const close = useCallback(() => {
    if (submitting) return;
    setOpen(false);
  }, [submitting]);

  const runCancel = useCallback(async () => {
    const trimmed = reason.trim();
    if (!trimmed) {
      setError("Nhập lý do hủy phiên.");
      return;
    }
    setSubmitting(true);
    setError(null);
    try {
      const res = await fetch(`/bff/document-matches/${matchId}/cancel`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Accept: "application/json",
        },
        body: JSON.stringify({ reason: trimmed }),
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
              ? "Phải đảo hết chi tiết hiệu lực trước khi hủy phiên."
              : "Hủy phiên khớp thất bại.")
        );
        return;
      }

      setOpen(false);
      startTransition(() => {
        router.push(`/documents/${documentId}`);
        router.refresh();
      });
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      setSubmitting(false);
    }
  }, [documentId, matchId, reason, router]);

  if (!canCancel) return null;

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
        {cancelLabel}
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
            <h2 id={dialogTitleId}>Hủy phiên khớp?</h2>
            <p>
              Chỉ hủy khi không còn chi tiết hiệu lực. Đảo các dòng khớp trước
              nếu cần.
            </p>
            <div className="field">
              <label htmlFor="cancel-match-reason">Lý do hủy</label>
              <input
                id="cancel-match-reason"
                type="text"
                value={reason}
                onChange={(e) => setReason(e.target.value)}
                maxLength={512}
                disabled={submitting}
                required
                placeholder="Ví dụ: mở nhầm phương thức"
              />
            </div>
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
                onClick={runCancel}
                disabled={submitting}
              >
                {submitting ? "Đang hủy…" : "Xác nhận hủy phiên"}
              </button>
            </div>
          </div>
        </div>
      ) : null}
    </>
  );
}
