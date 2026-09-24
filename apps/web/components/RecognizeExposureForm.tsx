"use client";

import type { FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { withIdempotency } from "@/lib/idempotency";
import { useIdempotency } from "@/lib/use-idempotency";
import { term, type TerminologyMap } from "@/lib/terminology";
import { formatMoney } from "@/lib/money";

type Kind = "payable" | "receivable";

type Props = {
  terms: TerminologyMap;
  kind: Kind;
  exposureId: string;
  openAmount: number;
  currencyCode: string;
  billId?: string | null;
  defaultDueDate?: string | null;
};

export function RecognizeExposureForm({
  terms,
  kind,
  exposureId,
  openAmount,
  currencyCode,
  billId,
  defaultDueDate,
}: Props) {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [isPending, startTransition] = useTransition();
  const isPayable = kind === "payable";
  const idem = useIdempotency(isPayable ? "recognize-ap" : "recognize-ar");
  const apLabel = term(terms, "ACCOUNTS_PAYABLE", "Khoản phải trả");
  const arLabel = term(terms, "ACCOUNTS_RECEIVABLE", "Khoản phải thu");
  const costLabel = term(terms, "COST", "Chi phí");
  const revenueLabel = term(terms, "REVENUE", "Doanh thu");
  const targetLabel = isPayable ? apLabel : arLabel;
  const economicLabel = isPayable ? costLabel : revenueLabel;

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const idemKey = idem.acquire();
    if (!idemKey) return;
    setError(null);
    setSubmitting(true);
    let succeeded = false;

    const fd = new FormData(e.currentTarget);
    const amountRaw = String(fd.get("amount") ?? "").trim();
    const amount = Number(amountRaw.replace(",", "."));
    if (!Number.isFinite(amount) || amount <= 0) {
      setError("Số tiền ghi nhận phải lớn hơn 0.");
      setSubmitting(false);
      idem.release(false);
      return;
    }
    if (amount > openAmount + 0.0000001) {
      setError(
        `Số ghi nhận không được vượt số còn mở (${formatMoney(openAmount, currencyCode)}).`
      );
      setSubmitting(false);
      idem.release(false);
      return;
    }

    const dueDateRaw = String(fd.get("dueDate") ?? "").trim();
    const body = {
      amount,
      dueDate: dueDateRaw || null,
      notes: String(fd.get("notes") ?? "").trim() || null,
    };

    const endpoint = isPayable
      ? `/bff/payable-exposures/${encodeURIComponent(exposureId)}/recognize`
      : `/bff/receivable-exposures/${encodeURIComponent(exposureId)}/recognize`;

    try {
      const res = await fetch(endpoint, {
        method: "POST",
        headers: withIdempotency(
          { "Content-Type": "application/json" },
          idemKey
        ),
        body: JSON.stringify(body),
      });

      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }

      if (!res.ok) {
        const payload = (await res.json().catch(() => ({}))) as {
          message?: string;
        };
        setError(
          payload.message ||
            (res.status === 403
              ? `Bạn không có quyền ghi nhận ${targetLabel.toLowerCase()}.`
              : res.status === 409
                ? "Không thể ghi nhận vì xung đột trạng thái. Tải lại và thử lại."
                : "Ghi nhận thất bại.")
        );
        return;
      }

      succeeded = true;
      if (billId) {
        startTransition(() => router.push(`/bills/${billId}`));
      } else {
        startTransition(() =>
          router.push(isPayable ? "/ap-ar" : "/ap-ar?tab=ar")
        );
      }
      router.refresh();
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      idem.release(succeeded);
      setSubmitting(false);
    }
  }

  const busy = submitting || isPending;
  const canRecognize = openAmount > 0;

  return (
    <form className="receive-form" onSubmit={onSubmit} noValidate>
      <p className="note">
        Ghi nhận tạo bản ghi {targetLabel} <strong>riêng</strong> — không đổi{" "}
        {economicLabel}. Có thể ghi nhận một phần (số còn mở{" "}
        {formatMoney(openAmount, currencyCode)}). Số dư còn lại (outstanding) do
        hệ thống tính — không nhập tay.
      </p>

      {!canRecognize ? (
        <div className="alert alert-error" role="status">
          Exposure đã ghi nhận hết hoặc không còn số mở.
        </div>
      ) : null}

      {error ? (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      ) : null}

      <div className="form-grid">
        <div className="field">
          <label htmlFor="amount">Số ghi nhận</label>
          <input
            id="amount"
            name="amount"
            type="number"
            inputMode="decimal"
            min={0}
            step="any"
            required
            disabled={busy || !canRecognize}
            defaultValue={canRecognize ? String(openAmount) : undefined}
          />
        </div>
        <div className="field">
          <label htmlFor="dueDate">Hạn (tuỳ chọn)</label>
          <input
            id="dueDate"
            name="dueDate"
            type="date"
            disabled={busy || !canRecognize}
            defaultValue={defaultDueDate ?? undefined}
          />
        </div>
        <div className="field field-span">
          <label htmlFor="notes">Ghi chú</label>
          <input
            id="notes"
            name="notes"
            maxLength={2048}
            disabled={busy || !canRecognize}
          />
        </div>
      </div>

      <div className="cta-row">
        <button
          type="submit"
          className="btn"
          disabled={busy || !canRecognize}
        >
          {busy
            ? "Đang ghi nhận…"
            : `Ghi nhận → ${targetLabel}`}
        </button>
      </div>
    </form>
  );
}
