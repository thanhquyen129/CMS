"use client";

import { useRouter } from "next/navigation";
import { useCallback, useId, useState, useTransition } from "react";
import { term, type TerminologyMap } from "@/lib/terminology";
import { formatMoney } from "@/lib/money";
import { newIdempotencyKey, withIdempotency } from "@/lib/idempotency";
import { formatHttpError, readApiErrorBody } from "@/lib/api-error";

type Props = {
  terms: TerminologyMap;
  allocationId: string;
  amount: number;
  currencyCode: string;
  billCount: number;
  /** When true, hide CTA — creator cannot self-finalize (PC-21 / W-L1). */
  blockedAsCreator?: boolean;
};

export function FinalizeCostAllocationButton({
  terms,
  allocationId,
  amount,
  currencyCode,
  billCount,
  blockedAsCreator = false,
}: Props) {
  const router = useRouter();
  const dialogTitleId = useId();
  const [open, setOpen] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [isPending, startTransition] = useTransition();

  const finalizeLabel = term(terms, "ALLOCATION_FINALIZED", "Đã chốt phân bổ");
  const billLabel = term(terms, "BILL", "Bill");
  const allocLabel = term(terms, "COST_ALLOCATION", "Phân bổ chi phí");

  const close = useCallback(() => {
    if (submitting) return;
    setOpen(false);
  }, [submitting]);

  const runFinalize = useCallback(async () => {
    setSubmitting(true);
    setError(null);

    try {
      const res = await fetch(`/bff/cost-allocations/${allocationId}/finalize`, {
        method: "POST",
        headers: withIdempotency({}, newIdempotencyKey("alloc-fin")),
      });

      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }

      if (!res.ok && res.status !== 204) {
        const body = await readApiErrorBody(res);
        setError(
          formatHttpError(res.status, body, {
            conflict:
              "Không chốt được (PC-21 / cơ sở / bảo toàn số tiền). Kiểm tra phiên phân bổ.",
            default: "Chốt phân bổ thất bại.",
          })
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
  }, [allocationId, router]);

  if (blockedAsCreator) {
    return (
      <span className="muted" role="status">
        Người tạo không được tự chốt (PC-21) — nhờ người khác chốt phiên này.
      </span>
    );
  }

  return (
    <>
      <button
        type="button"
        className="btn btn-sm"
        onClick={() => {
          setError(null);
          setOpen(true);
        }}
        disabled={isPending}
      >
        Chốt phân bổ
      </button>

      {error && !open ? (
        <div
          className="alert alert-error"
          role="alert"
          style={{ marginTop: "0.5rem" }}
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
            <h2 id={dialogTitleId}>Chốt {allocLabel.toLowerCase()}?</h2>
            <p>
              {formatMoney(amount, currencyCode)} sẽ được phân bổ sang {billCount}{" "}
              {billLabel}. Trạng thái → <strong>{finalizeLabel}</strong>. Phiên
              đã chốt trước (nếu có) chuyển thành đã thay thế — không ghi đè im
              lặng.
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
                onClick={runFinalize}
                disabled={submitting}
              >
                {submitting ? "Đang chốt…" : "Xác nhận chốt"}
              </button>
            </div>
          </div>
        </div>
      ) : null}
    </>
  );
}
