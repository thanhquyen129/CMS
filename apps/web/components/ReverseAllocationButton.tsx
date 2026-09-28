"use client";

import { useRouter } from "next/navigation";
import { useCallback, useId, useState, useTransition } from "react";
import { withRowVersion } from "@/lib/idempotency";
import { formatMoney } from "@/lib/money";
import { term, type TerminologyMap } from "@/lib/terminology";
import { BalanceImpact } from "./BalanceImpact";

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
  /** Current AR/AP balance — shows Before → After when in the same currency as the cash. */
  targetOutstanding?: number | null;
  targetCurrencyCode?: string | null;
};

export function ReverseAllocationButton({
  terms,
  kind,
  allocationId,
  amount,
  currencyCode,
  allocationStatus,
  rowVersion,
  targetOutstanding = null,
  targetCurrencyCode = null,
}: Props) {
  const router = useRouter();
  const dialogTitleId = useId();
  const [open, setOpen] = useState(false);
  const [reason, setReason] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [isPending, startTransition] = useTransition();

  const reverseLabel = term(terms, "ALLOCATION_REVERSED", "Đã hủy phân bổ");
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
      setError("Nhập lý do hủy phân bổ.");
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
              ? "Không hủy được (đã hủy / trạng thái lệch). Tải lại trang."
              : "Hủy phân bổ thất bại.")
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
        Hủy phân bổ
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
            <h2 id={dialogTitleId}>Hủy phân bổ?</h2>
            <p>
              {isDraft ? (
                <>
                  Hủy phân bổ nháp {formatMoney(amount, currencyCode)}. Số dư{" "}
                  {target} không đổi. Trạng thái →{" "}
                  <strong>{reverseLabel}</strong>.
                </>
              ) : (
                <>
                  {formatMoney(amount, currencyCode)} sẽ được hoàn lại vào số dư{" "}
                  {target}. Không xóa bản ghi; trạng thái →{" "}
                  <strong>{reverseLabel}</strong>.
                </>
              )}
            </p>
            {targetOutstanding != null &&
            targetCurrencyCode?.toUpperCase() === currencyCode.toUpperCase() ? (
              <BalanceImpact
                label={`Số dư ${target.toLowerCase()}`}
                before={targetOutstanding}
                after={isDraft ? targetOutstanding : targetOutstanding + amount}
                currencyCode={currencyCode}
              />
            ) : null}
            <div className="field">
              <label htmlFor={`rev-alloc-${allocationId}`}>Lý do hủy</label>
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
                {submitting ? "Đang hủy…" : "Xác nhận hủy phân bổ"}
              </button>
            </div>
          </div>
        </div>
      ) : null}
    </>
  );
}
