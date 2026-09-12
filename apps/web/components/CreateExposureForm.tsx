"use client";

import type { FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { term, type TerminologyMap } from "@/lib/terminology";

type Kind = "payable" | "receivable";

type Props = {
  terms: TerminologyMap;
  kind: Kind;
  defaultBillId?: string;
  defaultCurrency?: string;
  defaultAmount?: number;
};

export function CreateExposureForm({
  terms,
  kind,
  defaultBillId,
  defaultCurrency = "VND",
  defaultAmount,
}: Props) {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [isPending, startTransition] = useTransition();

  const isPayable = kind === "payable";
  const exposureLabel = isPayable
    ? term(terms, "PAYABLE_EXPOSURE", "Nghĩa vụ phải trả (exposure)")
    : term(terms, "RECEIVABLE_EXPOSURE", "Quyền thu dự kiến (exposure)");
  const apLabel = term(terms, "ACCOUNTS_PAYABLE", "Khoản phải trả");
  const arLabel = term(terms, "ACCOUNTS_RECEIVABLE", "Khoản phải thu");
  const billLabel = term(terms, "BILL", "Bill");
  const costLabel = term(terms, "COST", "Chi phí");
  const revenueLabel = term(terms, "REVENUE", "Doanh thu");
  const recognizedTarget = isPayable ? apLabel : arLabel;

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setError(null);
    setSubmitting(true);

    const fd = new FormData(e.currentTarget);
    const amountRaw = String(fd.get("amount") ?? "").trim();
    const amount = Number(amountRaw.replace(",", "."));
    if (!Number.isFinite(amount) || amount <= 0) {
      setError("Số tiền phải lớn hơn 0.");
      setSubmitting(false);
      return;
    }

    const billIdRaw = String(fd.get("billId") ?? "").trim();
    const effectiveDateRaw = String(fd.get("effectiveDate") ?? "").trim();
    const dueDateRaw = String(fd.get("dueDate") ?? "").trim();
    const body = {
      amount,
      currencyCode: String(fd.get("currencyCode") ?? "VND")
        .trim()
        .toUpperCase(),
      effectiveDate: effectiveDateRaw || null,
      dueDate: dueDateRaw || null,
      billId: billIdRaw || null,
      counterpartyId: null,
      costId: null,
      revenueId: null,
      financialDocumentId: null,
      notes: String(fd.get("notes") ?? "").trim() || null,
      sourceType: null,
      sourceId: null,
    };

    const endpoint = isPayable
      ? "/bff/payable-exposures"
      : "/bff/receivable-exposures";

    try {
      const res = await fetch(endpoint, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Accept: "application/json",
        },
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
              ? "Bạn không có quyền tạo exposure."
              : res.status === 409
                ? "Không thể tạo vì xung đột trạng thái."
                : "Tạo exposure thất bại.")
        );
        return;
      }

      const created = (await res.json().catch(() => ({}))) as { id?: string };
      if (created.id) {
        startTransition(() =>
          router.push(
            `/ap-ar/exposures/${created.id}/recognize?kind=${kind}`
          )
        );
      } else {
        startTransition(() => router.push("/ap-ar?tab=exposure"));
      }
      router.refresh();
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      setSubmitting(false);
    }
  }

  const busy = submitting || isPending;

  return (
    <form className="receive-form" onSubmit={onSubmit} noValidate>
      <p className="note">
        Exposure ≠ {recognizedTarget}. Đây là nghĩa vụ / quyền{" "}
        <strong>dự kiến</strong> — chưa phải {isPayable ? costLabel : revenueLabel}{" "}
        và chưa phải sổ đã ghi nhận. Sau khi tạo, ghi nhận (Recognize) sang{" "}
        {recognizedTarget}.
      </p>

      {error ? (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      ) : null}

      <div className="form-grid">
        <div className="field">
          <label htmlFor="amount">Số tiền</label>
          <input
            id="amount"
            name="amount"
            type="number"
            inputMode="decimal"
            min={0}
            step="any"
            required
            disabled={busy}
            defaultValue={
              defaultAmount != null && Number.isFinite(defaultAmount)
                ? String(defaultAmount)
                : undefined
            }
          />
        </div>
        <div className="field">
          <label htmlFor="currencyCode">Tiền tệ</label>
          <input
            id="currencyCode"
            name="currencyCode"
            defaultValue={defaultCurrency}
            maxLength={3}
            required
            disabled={busy}
          />
        </div>
        <div className="field">
          <label htmlFor="effectiveDate">Ngày hiệu lực</label>
          <input
            id="effectiveDate"
            name="effectiveDate"
            type="date"
            disabled={busy}
          />
        </div>
        <div className="field">
          <label htmlFor="dueDate">Hạn</label>
          <input id="dueDate" name="dueDate" type="date" disabled={busy} />
        </div>
        <div className="field">
          <label htmlFor="billId">{billLabel} (tuỳ chọn, UUID)</label>
          <input
            id="billId"
            name="billId"
            defaultValue={defaultBillId ?? ""}
            disabled={busy}
            placeholder="Gắn Bill nếu có"
            autoComplete="off"
          />
        </div>
        <div className="field field-span">
          <label htmlFor="notes">Ghi chú</label>
          <input id="notes" name="notes" maxLength={2048} disabled={busy} />
        </div>
      </div>

      <div className="cta-row">
        <button type="submit" className="btn" disabled={busy}>
          {busy ? "Đang tạo…" : `Tạo ${exposureLabel}`}
        </button>
      </div>
    </form>
  );
}
