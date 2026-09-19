"use client";

import type { FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";

type Props = {
  ruleId: string;
  defaultCurrency: string;
};

/** Adds a Cost/Revenue component on a draft pricing rule (UI-03). */
export function AddPricingRuleComponentForm({
  ruleId,
  defaultCurrency,
}: Props) {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [isPending, startTransition] = useTransition();
  const [nature, setNature] = useState("cost");

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setError(null);
    setSubmitting(true);
    const fd = new FormData(e.currentTarget);
    const amount = Number(String(fd.get("amount") ?? "").replace(",", "."));
    if (!Number.isFinite(amount) || amount < 0) {
      setError("Số tiền thành phần không hợp lệ.");
      setSubmitting(false);
      return;
    }

    const body = {
      code: String(fd.get("code") ?? "").trim(),
      name: String(fd.get("name") ?? "").trim(),
      financialNature: nature,
      costTypeCode:
        nature === "cost"
          ? String(fd.get("typeCode") ?? "").trim() || null
          : null,
      revenueTypeCode:
        nature === "revenue"
          ? String(fd.get("typeCode") ?? "").trim() || null
          : null,
      amount,
      currencyCode: String(fd.get("currencyCode") ?? defaultCurrency)
        .trim()
        .toUpperCase(),
      sortOrder: 0,
    };

    if (!body.code || !body.name) {
      setError("Nhập mã và tên thành phần.");
      setSubmitting(false);
      return;
    }

    try {
      const res = await fetch(`/bff/pricing-rules/${ruleId}/components`, {
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
            (res.status === 409
              ? "Chỉ thêm thành phần trên phiên bản nháp."
              : "Thêm thành phần thất bại.")
        );
        return;
      }
      startTransition(() => router.refresh());
      e.currentTarget.reset();
      setNature("cost");
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      setSubmitting(false);
    }
  }

  const busy = submitting || isPending;

  return (
    <form className="receive-form" onSubmit={onSubmit} noValidate>
      <h4 className="section-title sm">Thành phần giá</h4>
      {error ? (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      ) : null}
      <div className="form-grid">
        <div className="field">
          <label htmlFor={`comp-code-${ruleId}`}>Mã</label>
          <input
            id={`comp-code-${ruleId}`}
            name="code"
            required
            maxLength={64}
            disabled={busy}
          />
        </div>
        <div className="field">
          <label htmlFor={`comp-name-${ruleId}`}>Tên</label>
          <input
            id={`comp-name-${ruleId}`}
            name="name"
            required
            maxLength={256}
            disabled={busy}
          />
        </div>
        <div className="field">
          <label htmlFor={`comp-nature-${ruleId}`}>Tính chất</label>
          <select
            id={`comp-nature-${ruleId}`}
            value={nature}
            onChange={(e) => setNature(e.target.value)}
            disabled={busy}
          >
            <option value="cost">Chi phí</option>
            <option value="revenue">Doanh thu</option>
          </select>
        </div>
        <div className="field">
          <label htmlFor={`comp-type-${ruleId}`}>
            {nature === "revenue" ? "Loại doanh thu" : "Loại chi phí"}
          </label>
          <input
            id={`comp-type-${ruleId}`}
            name="typeCode"
            maxLength={64}
            disabled={busy}
            placeholder="VD: FREIGHT"
          />
        </div>
        <div className="field">
          <label htmlFor={`comp-amount-${ruleId}`}>Số tiền</label>
          <input
            id={`comp-amount-${ruleId}`}
            name="amount"
            inputMode="decimal"
            required
            disabled={busy}
          />
        </div>
        <div className="field">
          <label htmlFor={`comp-ccy-${ruleId}`}>Tiền tệ</label>
          <input
            id={`comp-ccy-${ruleId}`}
            name="currencyCode"
            defaultValue={defaultCurrency}
            maxLength={3}
            required
            disabled={busy}
          />
        </div>
      </div>
      <div className="cta-row">
        <button className="btn" type="submit" disabled={busy}>
          {busy ? "Đang thêm…" : "Thêm thành phần"}
        </button>
      </div>
    </form>
  );
}
