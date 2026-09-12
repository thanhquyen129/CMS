"use client";

import type { FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { term, type TerminologyMap } from "@/lib/terminology";

type Kind = "payment" | "collection";

type Props = {
  terms: TerminologyMap;
  kind: Kind;
  defaultBillId?: string;
  defaultCurrency?: string;
};

export function CreateCashTxnForm({
  terms,
  kind,
  defaultBillId,
  defaultCurrency = "VND",
}: Props) {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [isPending, startTransition] = useTransition();

  const isPayment = kind === "payment";
  const label = isPayment
    ? term(terms, "PAYMENT", "Thanh toán")
    : term(terms, "COLLECTION", "Thu tiền");
  const costLabel = term(terms, "COST", "Chi phí");
  const revenueLabel = term(terms, "REVENUE", "Doanh thu");
  const billLabel = term(terms, "BILL", "Bill");
  const apLabel = term(terms, "ACCOUNTS_PAYABLE", "Khoản phải trả");
  const arLabel = term(terms, "ACCOUNTS_RECEIVABLE", "Khoản phải thu");

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
    const valueDateRaw = String(fd.get("valueDate") ?? "").trim();
    const body = {
      amount,
      currencyCode: String(fd.get("currencyCode") ?? "VND")
        .trim()
        .toUpperCase(),
      valueDate: valueDateRaw || null,
      counterpartyId: null,
      billId: billIdRaw || null,
      referenceNo: String(fd.get("referenceNo") ?? "").trim() || null,
      notes: String(fd.get("notes") ?? "").trim() || null,
    };

    const endpoint = isPayment ? "/bff/payments" : "/bff/collections";
    const detailBase = isPayment
      ? "/settlements/payments"
      : "/settlements/collections";

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
              ? `Bạn không có quyền tạo ${label.toLowerCase()}.`
              : res.status === 409
                ? "Không thể tạo vì xung đột trạng thái (kỳ có thể đã khóa)."
                : `Tạo ${label.toLowerCase()} thất bại.`)
        );
        return;
      }

      const created = (await res.json().catch(() => ({}))) as { id?: string };
      if (created.id) {
        startTransition(() => router.push(`${detailBase}/${created.id}`));
      } else {
        startTransition(() =>
          router.push(
            isPayment ? "/settlements" : "/settlements?tab=collections"
          )
        );
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
        {isPayment ? (
          <>
            {label} ≠ {costLabel}. Ghi nhận tiền ra; chỉ giảm {apLabel} sau khi
            phân bổ và <strong>chốt phân bổ</strong>.
          </>
        ) : (
          <>
            {label} ≠ {revenueLabel}. Ghi nhận tiền vào; chỉ giảm {arLabel} sau
            khi phân bổ và <strong>chốt phân bổ</strong>.
          </>
        )}
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
          <label htmlFor="valueDate">Ngày giá trị</label>
          <input
            id="valueDate"
            name="valueDate"
            type="date"
            disabled={busy}
          />
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
        <div className="field">
          <label htmlFor="referenceNo">Số tham chiếu</label>
          <input
            id="referenceNo"
            name="referenceNo"
            maxLength={128}
            disabled={busy}
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
          {busy ? "Đang tạo…" : `Tạo ${label.toLowerCase()}`}
        </button>
      </div>
    </form>
  );
}
