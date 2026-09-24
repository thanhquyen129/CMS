"use client";

import type { FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useEffect, useState, useTransition } from "react";
import { BillTypeahead } from "@/components/BillTypeahead";
import { CatalogCodeSelect } from "@/components/CatalogCodeSelect";
import { formatMoney } from "@/lib/money";
import { formatApiErrorMessage } from "@/lib/api-error";
import { term, type TerminologyMap } from "@/lib/terminology";

type Props = {
  terms: TerminologyMap;
  documentId: string;
  currencyCode: string;
  documentTotal: number;
  linesSum: number;
  /** Prefill = max(0, total − sum lines); parity UAT first line = header. */
  defaultAmount: number;
  defaultBillId?: string | null;
  defaultBillNo?: string | null;
  direction?: string;
  formKey: string;
};

export function AddDocumentLineForm({
  terms,
  documentId,
  currencyCode,
  documentTotal,
  linesSum,
  defaultAmount,
  defaultBillId,
  defaultBillNo,
  direction,
  formKey,
}: Props) {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [isPending, startTransition] = useTransition();
  const [amountDraft, setAmountDraft] = useState(
    defaultAmount > 0 ? String(defaultAmount) : ""
  );
  const filled = defaultAmount <= 0.0000001;

  useEffect(() => {
    setAmountDraft(defaultAmount > 0 ? String(defaultAmount) : "");
  }, [formKey, defaultAmount]);

  const lineLabel = term(terms, "FINANCIAL_DOCUMENT_LINE", "Dòng chứng từ");
  const billLabel = term(terms, "BILL", "Bill");
  const costLabel = term(terms, "COST", "Chi phí");
  const revenueLabel = term(terms, "REVENUE", "Doanh thu");
  const matchedLabel = term(terms, "MATCHED", "Đã khớp");
  const isPayable = direction?.toLowerCase() === "payable";
  const isReceivable = direction?.toLowerCase() === "receivable";

  const amountNum = Number(String(amountDraft).replace(",", "."));
  const projectedSum =
    linesSum + (Number.isFinite(amountNum) && amountNum > 0 ? amountNum : 0);
  const overTotal =
    Number.isFinite(amountNum) &&
    amountNum > 0 &&
    projectedSum > Number(documentTotal) + 0.0000001;

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setError(null);
    setSuccess(null);
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
          errors?: Record<string, string[]>;
        };
        setError(
          formatApiErrorMessage(
            payload,
            res.status === 403
              ? `Bạn không có quyền thêm ${lineLabel.toLowerCase()}.`
              : res.status === 409
                ? "Không thêm được dòng (chứng từ chưa nhận / hết hiệu lực / tiền tệ lệch)."
                : `Thêm ${lineLabel.toLowerCase()} thất bại.`
          )
        );
        return;
      }

      setSuccess(`Đã thêm ${lineLabel.toLowerCase()}.`);
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
    <form
      key={formKey}
      className="receive-form"
      onSubmit={onSubmit}
      noValidate
    >
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
      {success ? (
        <div className="alert alert-info" role="status">
          {success}
        </div>
      ) : null}
      {filled ? (
        <div className="alert alert-info" role="status">
          Đã phân bổ đủ tổng chứng từ. Không thêm dòng mới.
        </div>
      ) : null}
      {!filled && overTotal ? (
        <div className="alert alert-error" role="alert">
          Tổng dòng sau khi thêm (
          {formatMoney(projectedSum, currencyCode)}) vượt tổng chứng từ (
          {formatMoney(documentTotal, currencyCode)}). Giảm số tiền hoặc sửa
          dòng hiện có.
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
            disabled={busy || filled}
            autoFocus
            value={filled ? "" : amountDraft}
            onChange={(ev) => setAmountDraft(ev.target.value)}
          />
          {defaultAmount > 0 ? (
            <p className="muted small">
              Gợi ý còn theo header:{" "}
              {formatMoney(defaultAmount, currencyCode)}
            </p>
          ) : null}
        </div>

        <div className="field field-span">
          <label htmlFor="description">Mô tả</label>
          <input
            id="description"
            name="description"
            maxLength={512}
            disabled={busy}
            autoComplete="off"
            placeholder="vd. cước vận chuyển"
          />
        </div>

        <BillTypeahead
          label={`${billLabel} (tuỳ chọn)`}
          disabled={busy || filled}
          defaultId={defaultBillId}
          defaultLabel={defaultBillNo}
        />

        {isPayable || !isReceivable ? (
          <CatalogCodeSelect
            id="costTypeCode"
            name="costTypeCode"
            kind="cost_type"
            label={`Loại ${costLabel.toLowerCase()}`}
            disabled={busy || filled}
          />
        ) : null}

        {isReceivable || !isPayable ? (
          <CatalogCodeSelect
            id="revenueTypeCode"
            name="revenueTypeCode"
            kind="revenue_type"
            label={`Loại ${revenueLabel.toLowerCase()}`}
            disabled={busy || filled}
          />
        ) : null}
      </div>

      <div className="cta-row">
        <button type="submit" className="btn" disabled={busy || overTotal || filled}>
          {busy ? "Đang thêm…" : `Thêm ${lineLabel.toLowerCase()}`}
        </button>
      </div>
    </form>
  );
}
