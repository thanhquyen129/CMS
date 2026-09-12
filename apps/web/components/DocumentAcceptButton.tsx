"use client";

import { useRouter } from "next/navigation";
import { useCallback, useId, useState, useTransition } from "react";
import { term, type TerminologyMap } from "@/lib/terminology";
import { formatMoney } from "@/lib/money";

type Props = {
  terms: TerminologyMap;
  documentId: string;
  documentNo: string;
  totalAmount: number;
  linesSum: number;
  currencyCode: string;
  canAccept: boolean;
};

export function DocumentAcceptButton({
  terms,
  documentId,
  documentNo,
  totalAmount,
  linesSum,
  currencyCode,
  canAccept,
}: Props) {
  const router = useRouter();
  const dialogTitleId = useId();
  const [open, setOpen] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [blocked, setBlocked] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [isPending, startTransition] = useTransition();

  const acceptLabel = term(terms, "ACCEPTED", "Đã chấp nhận");
  const docLabel = term(terms, "FINANCIAL_DOCUMENT", "Chứng từ tài chính");

  const close = useCallback(() => {
    if (submitting) return;
    setOpen(false);
  }, [submitting]);

  const runAccept = useCallback(async () => {
    setSubmitting(true);
    setError(null);
    try {
      const res = await fetch(`/bff/financial-documents/${documentId}/accept`, {
        method: "POST",
        headers: { Accept: "application/json" },
      });

      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }

      if (!res.ok) {
        const body = (await res.json().catch(() => ({}))) as { message?: string };
        const msg =
          body.message ||
          (res.status === 403
            ? "Bạn không có quyền chấp nhận chứng từ này."
            : res.status === 409
              ? "Không thể chấp nhận vì xung đột trạng thái. Tải lại trang và thử lại."
              : "Chấp nhận chứng từ thất bại.");
        setError(msg);
        if (res.status === 403 || res.status === 409) {
          setBlocked(true);
          setOpen(false);
          startTransition(() => router.refresh());
        }
        return;
      }

      setOpen(false);
      startTransition(() => router.refresh());
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      setSubmitting(false);
    }
  }, [documentId, router]);

  if (!canAccept || blocked) {
    return null;
  }

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
        Chấp nhận {docLabel.toLowerCase()}
      </button>

      {error && !open ? (
        <div className="alert alert-error" role="alert" style={{ marginTop: "0.75rem" }}>
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
            <h2 id={dialogTitleId}>Chấp nhận {docLabel.toLowerCase()}?</h2>
            <p>
              Số {documentNo} · tổng chứng từ{" "}
              {formatMoney(totalAmount, currencyCode)} · tổng dòng{" "}
              {formatMoney(linesSum, currencyCode)}. Thao tác chỉ đổi chiều{" "}
              <strong>{acceptLabel}</strong> — không đổi Nhận / Khớp, không tạo
              Chi phí hay Thanh toán. ADR-0012: tổng dòng phải bằng tổng chứng
              từ.
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
                onClick={runAccept}
                disabled={submitting}
              >
                {submitting ? "Đang chấp nhận…" : `Xác nhận — ${acceptLabel}`}
              </button>
            </div>
          </div>
        </div>
      ) : null}
    </>
  );
}
