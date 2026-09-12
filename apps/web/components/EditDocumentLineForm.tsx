"use client";

import type { FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { formatMoney } from "@/lib/money";
import { term, type TerminologyMap } from "@/lib/terminology";

type Props = {
  terms: TerminologyMap;
  documentId: string;
  lineId: string;
  lineNo: number;
  currencyCode: string;
  amount: number;
  description: string | null;
  billId: string | null;
  costTypeCode: string | null;
  revenueTypeCode: string | null;
  direction?: string;
  documentTotal: number;
  otherLinesSum: number;
  canDelete: boolean;
};

export function EditDocumentLineForm({
  terms,
  documentId,
  lineId,
  lineNo,
  currencyCode,
  amount,
  description,
  billId,
  costTypeCode,
  revenueTypeCode,
  direction,
  documentTotal,
  otherLinesSum,
  canDelete,
}: Props) {
  const router = useRouter();
  const [open, setOpen] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [isPending, startTransition] = useTransition();

  const lineLabel = term(terms, "FINANCIAL_DOCUMENT_LINE", "Dòng chứng từ");
  const billLabel = term(terms, "BILL", "Bill");
  const costLabel = term(terms, "COST", "Chi phí");
  const revenueLabel = term(terms, "REVENUE", "Doanh thu");
  const isPayable = direction?.toLowerCase() === "payable";
  const isReceivable = direction?.toLowerCase() === "receivable";

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setError(null);
    setSubmitting(true);

    const fd = new FormData(e.currentTarget);
    const amountRaw = String(fd.get("amount") ?? "").trim();
    const nextAmount = Number(amountRaw.replace(",", "."));
    if (!Number.isFinite(nextAmount) || nextAmount <= 0) {
      setError("Số tiền dòng phải lớn hơn 0.");
      setSubmitting(false);
      return;
    }

    if (otherLinesSum + nextAmount > Number(documentTotal) + 0.0000001) {
      setError(
        `Tổng dòng sẽ vượt tổng chứng từ (${formatMoney(documentTotal, currencyCode)}).`
      );
      setSubmitting(false);
      return;
    }

    const body = {
      amount: nextAmount,
      description: String(fd.get("description") ?? "").trim() || null,
      billId: String(fd.get("billId") ?? "").trim() || null,
      costTypeCode: String(fd.get("costTypeCode") ?? "").trim() || null,
      revenueTypeCode: String(fd.get("revenueTypeCode") ?? "").trim() || null,
    };

    try {
      const res = await fetch(
        `/bff/financial-documents/${documentId}/lines/${lineId}`,
        {
          method: "PUT",
          headers: {
            "Content-Type": "application/json",
            Accept: "application/json",
          },
          body: JSON.stringify(body),
        }
      );

      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }

      if (!res.ok) {
        const payload = (await res.json().catch(() => ({}))) as {
          message?: string;
        };
        setError(payload.message || `Sửa ${lineLabel.toLowerCase()} thất bại.`);
        return;
      }

      setOpen(false);
      startTransition(() => router.refresh());
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      setSubmitting(false);
    }
  }

  async function onDelete() {
    if (
      !window.confirm(
        `Xóa ${lineLabel.toLowerCase()} #${lineNo}? Chỉ dòng nháp chưa khớp.`
      )
    ) {
      return;
    }
    setError(null);
    setSubmitting(true);
    try {
      const res = await fetch(
        `/bff/financial-documents/${documentId}/lines/${lineId}`,
        { method: "DELETE", headers: { Accept: "application/json" } }
      );
      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }
      if (!res.ok) {
        const payload = (await res.json().catch(() => ({}))) as {
          message?: string;
        };
        setError(payload.message || `Xóa ${lineLabel.toLowerCase()} thất bại.`);
        return;
      }
      startTransition(() => router.refresh());
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      setSubmitting(false);
    }
  }

  const busy = submitting || isPending;

  if (!open) {
    return (
      <div className="cta-row" style={{ gap: "0.35rem", margin: 0 }}>
        <button
          type="button"
          className="btn btn-ghost btn-sm"
          disabled={busy}
          onClick={() => setOpen(true)}
        >
          Sửa
        </button>
        {canDelete ? (
          <button
            type="button"
            className="btn btn-ghost btn-sm"
            disabled={busy}
            onClick={() => void onDelete()}
          >
            Xóa
          </button>
        ) : null}
        {error ? (
          <span className="muted small" role="alert">
            {error}
          </span>
        ) : null}
      </div>
    );
  }

  return (
    <form className="receive-form" onSubmit={onSubmit} noValidate>
      <p className="note">
        Sửa dòng #{lineNo}. Tổng dòng không được vượt tổng chứng từ.
      </p>
      {error ? (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      ) : null}
      <div className="form-grid">
        <div className="field">
          <label htmlFor={`amt-${lineId}`}>Số tiền</label>
          <input
            id={`amt-${lineId}`}
            name="amount"
            type="number"
            inputMode="decimal"
            min={0}
            step="any"
            required
            disabled={busy}
            defaultValue={amount}
          />
        </div>
        <div className="field field-span">
          <label htmlFor={`desc-${lineId}`}>Mô tả</label>
          <input
            id={`desc-${lineId}`}
            name="description"
            maxLength={512}
            disabled={busy}
            defaultValue={description ?? ""}
          />
        </div>
        <div className="field">
          <label htmlFor={`bill-${lineId}`}>{billLabel} (UUID)</label>
          <input
            id={`bill-${lineId}`}
            name="billId"
            disabled={busy}
            defaultValue={billId ?? ""}
          />
        </div>
        {isPayable || !isReceivable ? (
          <div className="field">
            <label htmlFor={`cost-${lineId}`}>Mã loại {costLabel.toLowerCase()}</label>
            <input
              id={`cost-${lineId}`}
              name="costTypeCode"
              maxLength={64}
              disabled={busy}
              defaultValue={costTypeCode ?? ""}
            />
          </div>
        ) : null}
        {isReceivable || !isPayable ? (
          <div className="field">
            <label htmlFor={`rev-${lineId}`}>
              Mã loại {revenueLabel.toLowerCase()}
            </label>
            <input
              id={`rev-${lineId}`}
              name="revenueTypeCode"
              maxLength={64}
              disabled={busy}
              defaultValue={revenueTypeCode ?? ""}
            />
          </div>
        ) : null}
      </div>
      <div className="cta-row">
        <button type="submit" className="btn btn-sm" disabled={busy}>
          {busy ? "Đang lưu…" : "Lưu dòng"}
        </button>
        <button
          type="button"
          className="btn btn-ghost btn-sm"
          disabled={busy}
          onClick={() => setOpen(false)}
        >
          Hủy
        </button>
      </div>
    </form>
  );
}
