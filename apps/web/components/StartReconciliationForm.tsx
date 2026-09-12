"use client";

import type { FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { term, type TerminologyMap } from "@/lib/terminology";

type Props = {
  terms: TerminologyMap;
  defaultBillId?: string;
};

const TYPES = [
  { value: "manual", labelKey: "manual" },
  { value: "payment_ap", labelKey: "payment_ap" },
  { value: "collection_ar", labelKey: "collection_ar" },
  { value: "document", labelKey: "document" },
  { value: "cost_revenue", labelKey: "cost_revenue" },
] as const;

export function StartReconciliationForm({ terms, defaultBillId }: Props) {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [isPending, startTransition] = useTransition();

  const reconLabel = term(terms, "RECONCILIATION", "Đối soát");
  const billLabel = term(terms, "BILL", "Bill");

  function typeLabel(value: string): string {
    switch (value) {
      case "manual":
        return `${reconLabel} thủ công`;
      case "payment_ap":
        return "Thanh toán ↔ Phải trả";
      case "collection_ar":
        return "Thu tiền ↔ Phải thu";
      case "document":
        return term(terms, "FINANCIAL_DOCUMENT", "Chứng từ tài chính");
      case "cost_revenue":
        return "Chi phí ↔ Doanh thu";
      default:
        return value;
    }
  }

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setError(null);
    setSubmitting(true);

    const fd = new FormData(e.currentTarget);
    const billIdRaw = String(fd.get("billId") ?? "").trim();
    const body = {
      reconciliationType: String(fd.get("reconciliationType") ?? "manual").trim(),
      ruleCode: String(fd.get("ruleCode") ?? "").trim() || null,
      billId: billIdRaw || null,
      notes: String(fd.get("notes") ?? "").trim() || null,
    };

    try {
      const res = await fetch("/bff/reconciliations", {
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
              ? `Bạn không có quyền tạo phiên ${reconLabel.toLowerCase()}.`
              : `Tạo phiên ${reconLabel.toLowerCase()} thất bại.`)
        );
        return;
      }

      const payload = (await res.json()) as { id?: string };
      if (!payload.id) {
        setError("Máy chủ không trả mã phiên.");
        return;
      }

      startTransition(() => {
        router.push(`/reconciliations/${payload.id}`);
        router.refresh();
      });
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      setSubmitting(false);
    }
  }

  const busy = submitting || isPending;

  return (
    <form className="receive-form" onSubmit={onSubmit}>
      <p className="note">
        Mở phiên {reconLabel.toLowerCase()} thủ công. Chênh lệch (nếu có) ≠ ngoại
        lệ — ngoại lệ mở riêng.
      </p>
      <div className="form-grid">
        <div className="field">
          <label htmlFor="reconciliationType">Loại phiên</label>
          <select
            id="reconciliationType"
            name="reconciliationType"
            defaultValue="manual"
            disabled={busy}
            required
          >
            {TYPES.map((t) => (
              <option key={t.value} value={t.value}>
                {typeLabel(t.value)}
              </option>
            ))}
          </select>
        </div>
        <div className="field">
          <label htmlFor="billId">{billLabel} (tuỳ chọn)</label>
          <input
            id="billId"
            name="billId"
            type="text"
            placeholder="GUID Bill"
            defaultValue={defaultBillId ?? ""}
            disabled={busy}
            className="mono-id"
          />
        </div>
        <div className="field">
          <label htmlFor="ruleCode">Mã quy tắc (tuỳ chọn)</label>
          <input
            id="ruleCode"
            name="ruleCode"
            type="text"
            maxLength={128}
            disabled={busy}
          />
        </div>
        <div className="field field-span">
          <label htmlFor="notes">Ghi chú</label>
          <textarea
            id="notes"
            name="notes"
            rows={2}
            maxLength={2048}
            disabled={busy}
          />
        </div>
      </div>

      {error ? (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      ) : null}

      <div className="cta-row">
        <button type="submit" className="btn" disabled={busy}>
          {busy ? "Đang mở…" : `Mở phiên ${reconLabel.toLowerCase()}`}
        </button>
      </div>
    </form>
  );
}
