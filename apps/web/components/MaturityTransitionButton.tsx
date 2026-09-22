"use client";

import { useRouter } from "next/navigation";
import { useCallback, useId, useState, useTransition } from "react";
import { term, type TerminologyMap } from "@/lib/terminology";
import { formatMoney } from "@/lib/money";
import { newIdempotencyKey, withIdempotency } from "@/lib/idempotency";
import { formatHttpError, readApiErrorBody } from "@/lib/api-error";

type Kind = "cost" | "revenue";
type Action = "confirm" | "actualize";

type Props = {
  terms: TerminologyMap;
  kind: Kind;
  action: Action;
  lineId: string;
  currentAmount: number;
  currencyCode: string;
};

export function MaturityTransitionButton({
  terms,
  kind,
  action,
  lineId,
  currentAmount,
  currencyCode,
}: Props) {
  const router = useRouter();
  const dialogTitleId = useId();
  const [open, setOpen] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [isPending, startTransition] = useTransition();
  const [amount, setAmount] = useState(String(currentAmount));
  const [overrideReason, setOverrideReason] = useState("");

  const costLabel = term(terms, "COST", "Chi phí");
  const revenueLabel = term(terms, "REVENUE", "Doanh thu");
  const expected = term(terms, "EXPECTED", "Dự kiến");
  const confirmed = term(terms, "CONFIRMED", "Đã xác nhận");
  const actual = term(terms, "ACTUAL", "Thực tế");
  const lineLabel = kind === "cost" ? costLabel : revenueLabel;

  const copy =
    action === "confirm"
      ? {
          title: `Xác nhận ${lineLabel}`,
          body: `Chuyển ${expected} → ${confirmed}. Nhập số ghi vào lớp ${confirmed}. Lớp ${expected} giữ nguyên.`,
          amountLabel: `Số ${confirmed}`,
          cta: `Xác nhận ${lineLabel}`,
          btn: `Xác nhận ${lineLabel}`,
          btnClass: "btn btn-sm",
        }
      : {
          title: `Ghi nhận ${actual}`,
          body: `Chuyển ${confirmed} → ${actual}. Nhập số lớp ${actual}. Các lớp trước giữ nguyên.`,
          amountLabel: `Số ${actual}`,
          cta: `Ghi nhận ${actual}`,
          btn: `Ghi nhận ${actual}`,
          btnClass: "btn btn-ghost btn-sm",
        };

  const close = useCallback(() => {
    if (submitting) return;
    setOpen(false);
  }, [submitting]);

  const run = useCallback(async () => {
    setSubmitting(true);
    setError(null);
    const parsed = Number(String(amount).replace(",", "."));
    if (!Number.isFinite(parsed) || parsed < 0) {
      setError("Số tiền không hợp lệ.");
      setSubmitting(false);
      return;
    }

    const base =
      kind === "cost" ? `/bff/costs/${lineId}` : `/bff/revenues/${lineId}`;
    const path = action === "confirm" ? `${base}/confirm` : `${base}/actualize`;
    const body =
      action === "confirm"
        ? { confirmedAmount: parsed }
        : {
            actualAmount: parsed,
            overrideReason: kind === "revenue" && overrideReason.trim() ? overrideReason.trim() : null,
          };

    try {
      const res = await fetch(path, {
        method: "POST",
        headers: withIdempotency(
          { "Content-Type": "application/json" },
          newIdempotencyKey(`maturity-${action}`)
        ),
        body: JSON.stringify(body),
      });

      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }

      if (!res.ok) {
        const payload = await readApiErrorBody(res);
        setError(
          formatHttpError(res.status, payload, {
            conflict: "Không thể chuyển lớp. Tải lại trang và thử lại.",
            default: "Thao tác thất bại.",
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
  }, [action, amount, kind, lineId, overrideReason, router]);

  return (
    <>
      <button
        type="button"
        className={copy.btnClass}
        disabled={isPending}
        onClick={() => {
          setError(null);
          setAmount(String(currentAmount));
          setOpen(true);
        }}
      >
        {copy.btn}
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
            onClick={(e) => e.stopPropagation()}
          >
            <h2 id={dialogTitleId}>{copy.title}</h2>
            <p>
              {copy.body} Hiện tại:{" "}
              <strong>{formatMoney(currentAmount, currencyCode)}</strong>.
            </p>
            <div className="field">
              <label htmlFor={`maturity-amt-${lineId}`}>
                {copy.amountLabel} ({currencyCode})
              </label>
              <input
                id={`maturity-amt-${lineId}`}
                type="number"
                inputMode="decimal"
                min={0}
                step="any"
                value={amount}
                disabled={submitting}
                onChange={(e) => setAmount(e.target.value)}
              />
            </div>
            {kind === "revenue" && action === "actualize" ? (
              <div className="field">
                <label htmlFor={`maturity-reason-${lineId}`}>Lý do ghi đè nguồn ngoài</label>
                <input
                  id={`maturity-reason-${lineId}`}
                  value={overrideReason}
                  disabled={submitting}
                  onChange={(e) => setOverrideReason(e.target.value)}
                />
              </div>
            ) : null}
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
                onClick={() => void run()}
                disabled={submitting}
              >
                {submitting ? "Đang xử lý…" : copy.cta}
              </button>
            </div>
          </div>
        </div>
      ) : null}
    </>
  );
}
