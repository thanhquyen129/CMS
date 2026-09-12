"use client";

import type { FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { term, type TerminologyMap } from "@/lib/terminology";

type Props = {
  terms: TerminologyMap;
  documentId: string;
  currencyCode: string;
  defaultBillId?: string | null;
  direction?: string;
};

export function AddDocumentLineForm({
  terms,
  documentId,
  currencyCode,
  defaultBillId,
  direction,
}: Props) {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [isPending, startTransition] = useTransition();

  const lineLabel = term(terms, "FINANCIAL_DOCUMENT_LINE", "Dòng chứng từ");
  const billLabel = term(terms, "BILL", "Bill");
  const costLabel = term(terms, "COST", "Chi phí");
  const revenueLabel = term(terms, "REVENUE", "Doanh thu");
  const matchedLabel = term(terms, "MATCHED", "Đã khớp");
  const isPayable = direction?.toLowerCase() === "payable";
  const isReceivable = direction?.toLowerCase() === "receivable";

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setError(null);
    setSubmitting(true);

    const form = e.currentTarget;
    const fd = new FormData(form);
    const amountRaw = String(fd.get("amount") ?? "").trim();
    const amount = Number(amountRaw.replace(",", "."));
    if (!Number.isFinite(amount) || amount <= 0) {
      setError("Số tiền dòng phải lớn hơn 0.");
      setSubmitting(false);
      return;
    }

    const billIdRaw = String(fd.get("billId") ?? "").trim();
    const descriptionRaw = String(fd.get("description") ?? "").trim();
    const costTypeRaw = String(fd.get("costTypeCode") ?? "").trim();
    const revenueTypeRaw = String(fd.get("revenueTypeCode") ?? "").trim();

    const body = {
      amount,
      description: descriptionRaw || null,
      billId: billIdRaw || null,
      costTypeCode: costTypeRaw || null,
      revenueTypeCode: revenueTypeRaw || null,
      currencyCode: currencyCode.toUpperCase(),
    };

    try {
      const res = await fetch(`/bff/financial-documents/${documentId}/lines`, {
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
              ? `Bạn không có quyền thêm ${lineLabel.toLowerCase()}.`
              : res.status === 409
                ? "Không thêm được dòng (chứng từ chưa nhận / hết hiệu lực / tiền tệ lệch)."
                : `Thêm ${lineLabel.toLowerCase()} thất bại.`)
        );
        return;
      }

      form.reset();
      startTransition(() => {
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
    <form className="receive-form" onSubmit={onSubmit} noValidate>
      <p className="note">
        Thêm {lineLabel.toLowerCase()} để khớp sau — không tạo {costLabel}/
        {revenueLabel}. Tiền tệ cố định theo chứng từ ({currencyCode}). Số mở
        ban đầu = số tiền dòng; {matchedLabel.toLowerCase()} bắt đầu từ 0.
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
            autoFocus
          />
        </div>

        <div className="field field-span">
          <label htmlFor="description">Mô tả</label>
          <input
            id="description"
            name="description"
            maxLength={512}
            disabled={busy}
            autoComplete="off"
          />
        </div>

        <div className="field">
          <label htmlFor="billId">{billLabel} (tuỳ chọn, UUID)</label>
          <input
            id="billId"
            name="billId"
            defaultValue={defaultBillId ?? ""}
            disabled={busy}
            placeholder="Mặc định theo chứng từ nếu trống"
            autoComplete="off"
          />
        </div>

        {isPayable || !isReceivable ? (
          <div className="field">
            <label htmlFor="costTypeCode">Mã loại {costLabel.toLowerCase()}</label>
            <input
              id="costTypeCode"
              name="costTypeCode"
              maxLength={64}
              disabled={busy}
              autoComplete="off"
            />
          </div>
        ) : null}

        {isReceivable || !isPayable ? (
          <div className="field">
            <label htmlFor="revenueTypeCode">
              Mã loại {revenueLabel.toLowerCase()}
            </label>
            <input
              id="revenueTypeCode"
              name="revenueTypeCode"
              maxLength={64}
              disabled={busy}
              autoComplete="off"
            />
          </div>
        ) : null}
      </div>

      <div className="cta-row">
        <button type="submit" className="btn" disabled={busy}>
          {busy ? "Đang thêm…" : `Thêm ${lineLabel.toLowerCase()}`}
        </button>
      </div>
    </form>
  );
}
