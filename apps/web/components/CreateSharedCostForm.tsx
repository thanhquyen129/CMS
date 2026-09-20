"use client";

import type { FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { term, type TerminologyMap } from "@/lib/terminology";
import { PartyTypeahead } from "@/components/PartyTypeahead";

type Props = {
  terms: TerminologyMap;
  defaultCurrency?: string;
};

export function CreateSharedCostForm({
  terms,
  defaultCurrency = "VND",
}: Props) {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [isPending, startTransition] = useTransition();

  const costLabel = term(terms, "COST", "Chi phí");
  const sharedLabel = term(terms, "ATTRIBUTION_SHARED", "Chung");
  const billLabel = term(terms, "BILL", "Bill");
  const expected = term(terms, "EXPECTED", "Dự kiến");
  const allocLabel = term(terms, "COST_ALLOCATION", "Phân bổ chi phí");
  const paymentLabel = term(terms, "PAYMENT", "Thanh toán");

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setError(null);
    setSubmitting(true);

    const fd = new FormData(e.currentTarget);
    const amountRaw = String(fd.get("amount") ?? "").trim();
    const amount = Number(amountRaw.replace(",", "."));
    if (!Number.isFinite(amount) || amount < 0) {
      setError("Số tiền không hợp lệ.");
      setSubmitting(false);
      return;
    }

    const effectiveDateRaw = String(fd.get("effectiveDate") ?? "").trim();
    const body = {
      billId: null,
      attributionType: "shared",
      amount,
      currencyCode: String(fd.get("currencyCode") ?? "VND")
        .trim()
        .toUpperCase(),
      effectiveDate: effectiveDateRaw || null,
      costTypeCode: String(fd.get("costTypeCode") ?? "").trim() || null,
      vendorPartyId: String(fd.get("vendorPartyId") ?? "").trim() || null,
      sourceType: null,
      sourceId: null,
    };

    try {
      const res = await fetch("/bff/costs", {
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
              ? `Bạn không có quyền tạo ${costLabel.toLowerCase()}.`
              : res.status === 409
                ? "Không thể tạo vì xung đột (nguồn trùng hoặc kỳ khóa)."
                : `Tạo ${costLabel.toLowerCase()} thất bại.`)
        );
        return;
      }

      const created = (await res.json().catch(() => ({}))) as { id?: string };
      const next = created.id ? `/costs/shared/${created.id}` : "/costs/shared";
      startTransition(() => router.push(next));
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
        Tạo {costLabel.toLowerCase()} <strong>{sharedLabel.toLowerCase()}</strong> —{" "}
        <strong>không</strong> gắn {billLabel}. Sau đó {allocLabel.toLowerCase()} sang ≥2{" "}
        {billLabel}, rồi chốt. Bắt đầu ở lớp {expected}. {costLabel} ≠ {paymentLabel}.
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
              <label htmlFor="costTypeCode">Mã loại {costLabel.toLowerCase()}</label>
              <input
                id="costTypeCode"
                name="costTypeCode"
                maxLength={64}
                disabled={busy}
                placeholder="VD: SHARED-FUEL"
                autoComplete="off"
              />
            </div>
            <div className="field-span">
              <PartyTypeahead
                name="vendorPartyId"
                label="Nhà cung cấp"
                disabled={busy}
                hint="Không bắt buộc. Phải có vai trò nhà cung cấp hoặc bên nhận tiền."
              />
            </div>
          </div>
        </fieldset>
      </div>

      <div className="cta-row">
        <button type="submit" className="btn" disabled={busy}>
          {busy ? "Đang tạo…" : `Tạo ${costLabel.toLowerCase()} ${sharedLabel.toLowerCase()}`}
        </button>
      </div>
    </form>
  );
}
