"use client";

import { useRouter } from "next/navigation";
import { useCallback, useId, useState, useTransition } from "react";
import { withRowVersion } from "@/lib/idempotency";
import { formatMoney } from "@/lib/money";
import { term, type TerminologyMap } from "@/lib/terminology";

type Props = {
  terms: TerminologyMap;
  matchId: string;
  detailId: string;
  matchedAmount: number;
  currencyCode: string;
  rowVersion?: string | null;
};

export function ReverseMatchDetailButton({
  terms,
  matchId,
  detailId,
  matchedAmount,
  currencyCode,
  rowVersion,
}: Props) {
  const router = useRouter();
  const dialogTitleId = useId();
  const [open, setOpen] = useState(false);
  const [reason, setReason] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [isPending, startTransition] = useTransition();

  const reverseLabel = term(terms, "REVERSE_MATCH", "Hủy khớp");

  const close = useCallback(() => {
    if (submitting) return;
    setOpen(false);
  }, [submitting]);

  const runReverse = useCallback(async () => {
    const trimmed = reason.trim();
    if (!trimmed) {
      setError("Nhập lý do hủy khớp.");
      return;
    }
    setSubmitting(true);
    setError(null);
    try {
      const res = await fetch(
        `/bff/document-matches/${matchId}/details/${detailId}/reverse`,
        {
          method: "POST",
          headers: withRowVersion(
            {
              "Content-Type": "application/json",
              Accept: "application/json",
            },
            rowVersion
          ),
          body: JSON.stringify({ reason: trimmed }),
        }
      );

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
              ? "Không hủy được (đã hủy / phiên hủy). Tải lại trang."
              : "Hủy chi tiết khớp thất bại.")
        );
        return;
      }

      setOpen(false);
      setReason("");
      startTransition(() => router.refresh());
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      setSubmitting(false);
    }
  }, [detailId, matchId, reason, router, rowVersion]);

  return (
    <>
      <button
        type="button"
        className="btn btn-ghost btn-sm"
        onClick={() => {
          setError(null);
          setOpen(true);
        }}
        disabled={isPending}
      >
        {reverseLabel}
      </button>

      {error && !open ? (
        <div
          className="alert alert-error"
          role="alert"
          style={{ marginTop: "0.35rem" }}
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
            <h2 id={dialogTitleId}>Hủy chi tiết khớp?</h2>
            <p>
              Số tiền {formatMoney(matchedAmount, currencyCode)} sẽ trả lại số
              mở của dòng. Không xóa cứng; trạng thái → đã hủy khớp.
            </p>
            <div className="field">
              <label htmlFor={`rev-reason-${detailId}`}>Lý do hủy</label>
              <input
                id={`rev-reason-${detailId}`}
                type="text"
                value={reason}
                onChange={(e) => setReason(e.target.value)}
                maxLength={512}
                disabled={submitting}
                required
                placeholder="Ví dụ: khớp nhầm dòng"
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
                Hủy
              </button>
              <button
                type="button"
                className="btn"
                onClick={runReverse}
                disabled={submitting}
              >
                {submitting ? "Đang hủy…" : "Xác nhận hủy khớp"}
              </button>
            </div>
          </div>
        </div>
      ) : null}
    </>
  );
}
