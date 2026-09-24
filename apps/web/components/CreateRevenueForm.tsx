"use client";

import type { FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { CurrencySelect } from "@/components/CurrencySelect";
import { PartyTypeahead } from "@/components/PartyTypeahead";
import { withIdempotency } from "@/lib/idempotency";
import { useIdempotency } from "@/lib/use-idempotency";
import { formatApiErrorMessage, readApiErrorBody } from "@/lib/api-error";
import { term, type TerminologyMap } from "@/lib/terminology";

type Props = {
  terms: TerminologyMap;
  billId: string;
  defaultCurrency?: string;
};

export function CreateRevenueForm({
  terms,
  billId,
  defaultCurrency = "VND",
}: Props) {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [isPending, startTransition] = useTransition();
  const idem = useIdempotency("rev-create");

  const revenueLabel = term(terms, "REVENUE", "Doanh thu");
  const billLabel = term(terms, "BILL", "Bill");
  const expected = term(terms, "EXPECTED", "Dự kiến");
  const collectionLabel = term(terms, "COLLECTION", "Thu tiền");
  const arLabel = term(terms, "ACCOUNTS_RECEIVABLE", "Khoản phải thu");

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
    if (!Number.isFinite(amount) || amount < 0) {
      setError("Số tiền không hợp lệ.");
      setSubmitting(false);
      idem.release(false);
      return;
    }

    const effectiveDateRaw = String(fd.get("effectiveDate") ?? "").trim();
    const customerPartyId =
      String(fd.get("customerPartyId") ?? "").trim() || null;
    const body = {
      billId,
      amount,
      currencyCode: String(fd.get("currencyCode") ?? "VND")
        .trim()
        .toUpperCase(),
      effectiveDate: effectiveDateRaw || null,
      revenueTypeCode: String(fd.get("revenueTypeCode") ?? "").trim() || null,
      customerPartyId,
      sourceType: null,
      sourceId: null,
      recognitionPolicyVersion: null,
    };

    try {
      const res = await fetch("/bff/revenues", {
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
        const payload = await readApiErrorBody(res);
        setError(
          formatApiErrorMessage(
            payload,
            res.status === 403
              ? `Bạn không có quyền tạo ${revenueLabel.toLowerCase()}.`
              : res.status === 409
                ? "Không thể tạo vì xung đột trạng thái."
                : `Tạo ${revenueLabel.toLowerCase()} thất bại.`
          )
        );
        return;
      }

      succeeded = true;
      startTransition(() => router.push(`/bills/${billId}`));
      router.refresh();
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      idem.release(succeeded);
      setSubmitting(false);
    }
  }

  const busy = submitting || isPending;

  return (
    <form className="receive-form" onSubmit={onSubmit} noValidate>
      <p className="note">
        Tạo {revenueLabel.toLowerCase()} gắn {billLabel}, bắt đầu ở lớp {expected}.{" "}
        {revenueLabel} ≠ {collectionLabel} ≠ {arLabel}. Xác nhận trên hồ sơ Bill
        sau khi tạo.
      </p>

      {error ? (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      ) : null}

      <div className="form-sections cols-2">
        <fieldset className="group-box">
          <legend>Số tiền</legend>
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
            <CurrencySelect
              id="currencyCode"
              defaultValue={defaultCurrency}
              disabled={busy}
            />
          </div>
        </fieldset>
        <fieldset className="group-box">
          <legend>Phân loại</legend>
          <div className="form-grid">
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
              <label htmlFor="revenueTypeCode">
                Mã loại {revenueLabel.toLowerCase()}
              </label>
              <input
                id="revenueTypeCode"
                name="revenueTypeCode"
                maxLength={64}
                disabled={busy}
                placeholder="VD: FREIGHT"
                autoComplete="off"
              />
            </div>
            <PartyTypeahead
              name="customerPartyId"
              label="Khách hàng"
              disabled={busy}
              hint="Cần vai trò khách hàng hoặc bên trả tiền."
            />
          </div>
        </fieldset>
      </div>

      <div className="cta-row">
        <button type="submit" className="btn" disabled={busy}>
          {busy ? "Đang tạo…" : `Tạo ${revenueLabel.toLowerCase()}`}
        </button>
      </div>
    </form>
  );
}
