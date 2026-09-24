"use client";

import { useRouter } from "next/navigation";
import { useCallback, useId, useState, useTransition } from "react";
import { withRowVersion } from "@/lib/idempotency";
import { formatMoney } from "@/lib/money";
import { term, type TerminologyMap } from "@/lib/terminology";

type Kind = "payment" | "collection";

type Props = {
  terms: TerminologyMap;
  kind: Kind;
  allocationId: string;
  amount: number;
  currencyCode: string;
  /** draft | finalized — copy differs slightly */
  allocationStatus: string;
  rowVersion?: string | null;
};

export function ReverseAllocationButton({
  terms,
  kind,
  allocationId,
  amount,
  currencyCode,
  allocationStatus,
  rowVersion,
}: Props) {
  const router = useRouter();
  const dialogTitleId = useId();
  const [open, setOpen] = useState(false);
  const [reason, setReason] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [isPending, startTransition] = useTransition();

  const reverseLabel = term(terms, "ALLOCATION_REVERSED", "Đã đảo phân bổ");
  const apLabel = term(terms, "ACCOUNTS_PAYABLE", "Khoản phải trả");
  const arLabel = term(terms, "ACCOUNTS_RECEIVABLE", "Khoản phải thu");
  const target = kind === "payment" ? apLabel : arLabel;
  const isDraft = allocationStatus?.toLowerCase() === "draft";

  const close = useCallback(() => {
    if (submitting) return;
    setOpen(false);
  }, [submitting]);

  const runReverse = useCallback(async () => {
    const trimmed = reason.trim();
    if (!trimmed) {
      setError("Nhập lý do đảo phân bổ.");
      return;
    }
    setSubmitting(true);
    setError(null);
    const endpoint =
      kind === "payment"
        ? `/bff/payment-allocations/${allocationId}/reverse`
        : `/bff/collection-allocations/${allocationId}/reverse`;

    try {
      const res = await fetch(endpoint, {
        method: "POST",
        headers: withRowVersion(
          { "Content-Type": "application/json", Accept: "application/json" },
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
        setError(
          body.message ||
            (res.status === 409
              ? "Không đảo được (đã đảo / trạng thái lệch). Tải lại trang."
              : "Đảo phân bổ thất bại.")
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
  }, [allocationId, kind, reason, router]);

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
        Đảo phân bổ
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
            <h2 id={dialogTitleId}>Đảo phân bổ?</h2>
            <p>
              {isDraft ? (
                <>
                  Hủy phân bổ nháp {formatMoney(amount, currencyCode)}. Không
                  đụng outstanding {target}. Trạng thái →{" "}
                  <strong>{reverseLabel}</strong>.
                </>
              ) : (
                <>
                  {formatMoney(amount, currencyCode)} sẽ trả lại outstanding{" "}
                  {target}. Không xóa cứng; trạng thái →{" "}
                  <strong>{reverseLabel}</strong>.
                </>
              )}
            </p>
            <div className="field">
              <label htmlFor={`rev-alloc-${allocationId}`}>Lý do đảo</label>
              <input
                id={`rev-alloc-${allocationId}`}
                type="text"
                value={reason}
                onChange={(e) => setReason(e.target.value)}
                maxLength={1024}
                disabled={submitting}
                required
                placeholder="Ví dụ: phân bổ nhầm đối tượng"
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
                {submitting ? "Đang đảo…" : "Xác nhận đảo"}
              </button>
            </div>
          </div>
        </div>
      ) : null}
    </>
  );
}
