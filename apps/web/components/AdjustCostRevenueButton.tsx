"use client";

import { useRouter } from "next/navigation";
import { useCallback, useId, useState, useTransition } from "react";
import { term, type TerminologyMap } from "@/lib/terminology";
import { formatMoney } from "@/lib/money";
import { maturityLabelKey } from "@/lib/costs-revenues";

type Kind = "cost" | "revenue";

type Props = {
  terms: TerminologyMap;
  kind: Kind;
  lineId: string;
  currentAmount: number;
  currencyCode: string;
  financialMaturity: string;
  /** Compact trigger on list rows */
  buttonClassName?: string;
};

export function AdjustCostRevenueButton({
  terms,
  kind,
  lineId,
  currentAmount,
  currencyCode,
  financialMaturity,
  buttonClassName = "btn btn-ghost btn-sm",
}: Props) {
  const router = useRouter();
  const dialogTitleId = useId();
  const [open, setOpen] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [isPending, startTransition] = useTransition();
  const [adjustmentType, setAdjustmentType] = useState<"adjustment" | "reversal">(
    "adjustment"
  );
  const [deltaAmount, setDeltaAmount] = useState("");
  const [reason, setReason] = useState("");
  const [effectiveDate, setEffectiveDate] = useState("");

  const costLabel = term(terms, "COST", "Chi phí");
  const revenueLabel = term(terms, "REVENUE", "Doanh thu");
  const lineLabel = kind === "cost" ? costLabel : revenueLabel;
  const maturityKey = maturityLabelKey(financialMaturity);
  const maturityVi =
    maturityKey === "EXPECTED"
      ? term(terms, "EXPECTED", "Dự kiến")
      : maturityKey === "CONFIRMED"
        ? term(terms, "CONFIRMED", "Đã xác nhận")
        : maturityKey === "ACTUAL"
          ? term(terms, "ACTUAL", "Thực tế")
          : financialMaturity;

  const close = useCallback(() => {
    if (submitting) return;
    setOpen(false);
    setError(null);
  }, [submitting]);

  const runAdjust = useCallback(async () => {
    setSubmitting(true);
    setError(null);

    const delta = Number(String(deltaAmount).replace(",", "."));
    if (!Number.isFinite(delta) || delta === 0) {
      setError("Số điều chỉnh phải khác 0.");
      setSubmitting(false);
      return;
    }
    const reasonTrim = reason.trim();
    if (!reasonTrim) {
      setError("Phải nêu lý do điều chỉnh.");
      setSubmitting(false);
      return;
    }

    const endpoint =
      kind === "cost"
        ? `/bff/costs/${lineId}/adjustments`
        : `/bff/revenues/${lineId}/adjustments`;

    try {
      const res = await fetch(endpoint, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Accept: "application/json",
        },
        body: JSON.stringify({
          adjustmentType,
          deltaAmount: delta,
          reason: reasonTrim,
          effectiveDate: effectiveDate.trim() || null,
        }),
      });

      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }

      if (!res.ok) {
        const body = (await res.json().catch(() => ({}))) as {
          message?: string;
        };
        setError(
          body.message ||
            (res.status === 403
              ? `Bạn không có quyền điều chỉnh ${lineLabel.toLowerCase()}.`
              : res.status === 409
                ? "Không điều chỉnh được (trạng thái / số âm / kỳ khóa). Tải lại trang."
                : "Điều chỉnh thất bại.")
        );
        return;
      }

      setOpen(false);
      setDeltaAmount("");
      setReason("");
      setEffectiveDate("");
      setAdjustmentType("adjustment");
      startTransition(() => router.refresh());
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      setSubmitting(false);
    }
  }, [
    adjustmentType,
    deltaAmount,
    effectiveDate,
    kind,
    lineId,
    lineLabel,
    reason,
    router,
  ]);

  const previewAfter = (() => {
    const delta = Number(String(deltaAmount).replace(",", "."));
    if (!Number.isFinite(delta) || delta === 0) return null;
    let applied = delta;
    if (adjustmentType === "reversal" && applied > 0) applied = -applied;
    return currentAmount + applied;
  })();

  return (
    <>
      <button
        type="button"
        className={buttonClassName}
        disabled={isPending}
        onClick={() => {
          setError(null);
          setOpen(true);
        }}
      >
        Điều chỉnh
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
            <h2 id={dialogTitleId}>Điều chỉnh {lineLabel.toLowerCase()}</h2>
            <p>
              Áp delta vào lớp <strong>{maturityVi}</strong> hiện tại (
              {formatMoney(currentAmount, currencyCode)}). Ghi lịch sử điều chỉnh —
              không ghi đè im lặng các lớp trưởng thành khác (C-009).
            </p>

            <fieldset className="field" disabled={submitting}>
              <legend>Loại</legend>
              <div className="radio-row">
                <label>
                  <input
                    type="radio"
                    name="adj-type"
                    checked={adjustmentType === "adjustment"}
                    onChange={() => setAdjustmentType("adjustment")}
                  />{" "}
                  Điều chỉnh
                </label>
                <label>
                  <input
                    type="radio"
                    name="adj-type"
                    checked={adjustmentType === "reversal"}
                    onChange={() => setAdjustmentType("reversal")}
                  />{" "}
                  Đảo / hoàn (số dương → trừ)
                </label>
              </div>
            </fieldset>

            <div className="field">
              <label htmlFor={`adj-delta-${lineId}`}>
                Số delta ({currencyCode})
              </label>
              <input
                id={`adj-delta-${lineId}`}
                type="number"
                inputMode="decimal"
                step="any"
                value={deltaAmount}
                disabled={submitting}
                onChange={(e) => setDeltaAmount(e.target.value)}
                placeholder="VD: 50000 hoặc -20000"
              />
            </div>

            <div className="field">
              <label htmlFor={`adj-reason-${lineId}`}>Lý do</label>
              <textarea
                id={`adj-reason-${lineId}`}
                rows={3}
                maxLength={1024}
                value={reason}
                disabled={submitting}
                onChange={(e) => setReason(e.target.value)}
                required
                placeholder="Bắt buộc — ghi rõ vì sao đổi số"
              />
            </div>

            <div className="field">
              <label htmlFor={`adj-date-${lineId}`}>Ngày hiệu lực (tuỳ chọn)</label>
              <input
                id={`adj-date-${lineId}`}
                type="date"
                value={effectiveDate}
                disabled={submitting}
                onChange={(e) => setEffectiveDate(e.target.value)}
              />
            </div>

            {previewAfter != null ? (
              <p className="note">
                Sau điều chỉnh ≈{" "}
                <strong>
                  {previewAfter < 0
                    ? "âm (API từ chối)"
                    : formatMoney(previewAfter, currencyCode)}
                </strong>
              </p>
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
                onClick={() => void runAdjust()}
                disabled={submitting}
              >
                {submitting ? "Đang ghi…" : "Ghi điều chỉnh"}
              </button>
            </div>
          </div>
        </div>
      ) : null}
    </>
  );
}
