"use client";

import { useRouter } from "next/navigation";
import { useCallback, useId, useState, useTransition } from "react";
import { term, type TerminologyMap } from "@/lib/terminology";
import { formatMoney } from "@/lib/money";
import { withIdempotency, withRowVersion } from "@/lib/idempotency";
import { useIdempotency } from "@/lib/use-idempotency";
import { formatHttpError, readApiErrorBody } from "@/lib/api-error";

type Kind = "payment" | "collection";

type Props = {
  terms: TerminologyMap;
  kind: Kind;
  allocationId: string;
  amount: number;
  currencyCode: string;
  rowVersion?: string | null;
};

export function FinalizeAllocationButton({
  terms,
  kind,
  allocationId,
  amount,
  currencyCode,
  rowVersion,
}: Props) {
  const router = useRouter();
  const dialogTitleId = useId();
  const [open, setOpen] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [isPending, startTransition] = useTransition();
  const idem = useIdempotency("cash-alloc-fin");

  const finalizeLabel = term(terms, "ALLOCATION_FINALIZED", "Đã chốt phân bổ");
  const apLabel = term(terms, "ACCOUNTS_PAYABLE", "Khoản phải trả");
  const arLabel = term(terms, "ACCOUNTS_RECEIVABLE", "Khoản phải thu");
  const target = kind === "payment" ? apLabel : arLabel;

  const close = useCallback(() => {
    if (submitting) return;
    setOpen(false);
  }, [submitting]);

  const runFinalize = useCallback(async () => {
    const idemKey = idem.acquire();
    if (!idemKey) return;
    setSubmitting(true);
    setError(null);
    let succeeded = false;
    const endpoint =
      kind === "payment"
        ? `/bff/payment-allocations/${allocationId}/finalize`
        : `/bff/collection-allocations/${allocationId}/finalize`;

    try {
      const res = await fetch(endpoint, {
        method: "POST",
        headers: withRowVersion(withIdempotency({}, idemKey), rowVersion),
      });

      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }

      if (!res.ok && res.status !== 204) {
        const body = await readApiErrorBody(res);
        setError(
          formatHttpError(res.status, body, {
            conflict: "Không chốt được (trạng thái lệch). Tải lại trang.",
            default: "Chốt phân bổ thất bại.",
          })
        );
        return;
      }

      succeeded = true;
      setOpen(false);
      startTransition(() => router.refresh());
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      idem.release(succeeded);
      setSubmitting(false);
    }
  }, [allocationId, idem, kind, router, rowVersion]);

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
            <h2 id={dialogTitleId}>Chốt phân bổ?</h2>
            <p>
              {formatMoney(amount, currencyCode)} sẽ giảm outstanding {target}.
              Trạng thái → <strong>{finalizeLabel}</strong>. Đảo phân bổ là
              bước riêng (nút Đảo phân bổ).
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
