"use client";

import { useRouter } from "next/navigation";
import { useCallback, useId, useState, useTransition } from "react";
import { withRowVersion } from "@/lib/idempotency";
import { formatMoney } from "@/lib/money";
import { term, type TerminologyMap } from "@/lib/terminology";

type Kind = "payable" | "receivable";

type Props = {
  terms: TerminologyMap;
  kind: Kind;
  accountsId: string;
  outstanding: number;
  currencyCode: string;
  settledAmount: number;
  recordStatus: string;
  rowVersion?: string | null;
};

export function ReverseRecognizeButton({
  terms,
  kind,
  accountsId,
  outstanding,
  currencyCode,
  settledAmount,
  recordStatus,
  rowVersion,
}: Props) {
  const router = useRouter();
  const dialogTitleId = useId();
  const [open, setOpen] = useState(false);
  const [reason, setReason] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [isPending, startTransition] = useTransition();

  const label = term(terms, "REVERSE_RECOGNIZE", "Hủy ghi nhận");
  const canReverse =
    recordStatus?.toLowerCase() === "active" &&
    settledAmount <= 0 &&
    outstanding > 0;

  const close = useCallback(() => {
    if (submitting) return;
    setOpen(false);
  }, [submitting]);

  const run = useCallback(async () => {
    const trimmed = reason.trim();
    if (!trimmed) {
      setError("Nhập lý do hủy ghi nhận.");
      return;
    }
    setSubmitting(true);
    setError(null);
    const endpoint =
      kind === "payable"
        ? `/bff/accounts-payable/${accountsId}/reverse-recognize`
        : `/bff/accounts-receivable/${accountsId}/reverse-recognize`;
    try {
      const res = await fetch(endpoint, {
        method: "POST",
        headers: withRowVersion(
          {
            "Content-Type": "application/json",
            Accept: "application/json",
          },
          rowVersion
        ),
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
        setError(body.message || "Hủy ghi nhận thất bại.");
        return;
      }
      setOpen(false);
      startTransition(() => router.refresh());
    } catch {
      setError("Không kết nối được máy chủ.");
    } finally {
      setSubmitting(false);
    }
  }, [accountsId, kind, reason, router, rowVersion]);

  if (!canReverse) return null;

  return (
    <>
      <button
        type="button"
        className="btn btn-ghost btn-sm"
        disabled={isPending}
        onClick={() => {
          setError(null);
          setOpen(true);
        }}
      >
        {label}
      </button>
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
            <h2 id={dialogTitleId}>Hủy ghi nhận?</h2>
            <p>
              Soft-reverse số dư {formatMoney(outstanding, currencyCode)}. Không
              xóa lịch sử ghi nhận; exposure mở lại phần tương ứng. Phải hủy
              phân bổ đã chốt trước (nếu có).
            </p>
            <div className="field">
              <label htmlFor="rev-rec-reason">Lý do</label>
              <input
                id="rev-rec-reason"
                type="text"
                value={reason}
                onChange={(e) => setReason(e.target.value)}
                maxLength={1024}
                disabled={submitting}
                required
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
                onClick={run}
                disabled={submitting}
              >
                {submitting ? "Đang hủy…" : "Xác nhận hủy ghi nhận"}
              </button>
            </div>
          </div>
        </div>
      ) : null}
    </>
  );
}
