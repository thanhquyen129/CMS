"use client";

import type { FormEvent } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { term, type TerminologyMap } from "@/lib/terminology";

type ReconPickOption = {
  id: string;
  label: string;
  amount?: number;
  currencyCode?: string;
};

type Props = {
  terms: TerminologyMap;
  reconciliationId: string;
  bankLines?: ReconPickOption[];
  payments?: ReconPickOption[];
  collections?: ReconPickOption[];
};

const SOURCE_TYPES = [
  "payment",
  "collection",
  "cost",
  "revenue",
  "document",
  "accounts_payable",
  "accounts_receivable",
  "bank_line",
  "other",
] as const;

function objectLabel(terms: TerminologyMap, t: string): string {
  switch (t) {
    case "payment":
      return term(terms, "PAYMENT", "Thanh toán");
    case "collection":
      return term(terms, "COLLECTION", "Thu tiền");
    case "cost":
      return term(terms, "COST", "Chi phí");
    case "revenue":
      return term(terms, "REVENUE", "Doanh thu");
    case "document":
      return term(terms, "FINANCIAL_DOCUMENT", "Chứng từ tài chính");
    case "accounts_payable":
      return term(terms, "ACCOUNTS_PAYABLE", "Khoản phải trả");
    case "accounts_receivable":
      return term(terms, "ACCOUNTS_RECEIVABLE", "Khoản phải thu");
    case "bank_line":
      return term(terms, "BANK_FEED_LINE", "Dòng sao kê");
    default:
      return "Khác";
  }
}

export function AddReconciliationDetailForm({
  terms,
  reconciliationId,
  bankLines = [],
  payments = [],
  collections = [],
}: Props) {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [isPending, startTransition] = useTransition();

  const detailLabel = term(terms, "RECONCILIATION_DETAIL", "Chi tiết đối soát");
  const varianceLabel = term(terms, "VARIANCE", "Chênh lệch");

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setError(null);
    setSubmitting(true);

    const fd = new FormData(e.currentTarget);
    const sourceId = String(fd.get("sourceId") ?? "").trim();
    const targetTypeRaw = String(fd.get("targetType") ?? "").trim();
    const targetIdRaw = String(fd.get("targetId") ?? "").trim();
    const sourceAmount = Number(
      String(fd.get("sourceAmount") ?? "").replace(",", ".")
    );
    const targetAmount = Number(
      String(fd.get("targetAmount") ?? "").replace(",", ".")
    );
    const matchedAmount = Number(
      String(fd.get("matchedAmount") ?? "").replace(",", ".")
    );

    if (!sourceId) {
      setError("Nhập mã đối tượng nguồn.");
      setSubmitting(false);
      return;
    }
    if (!Number.isFinite(sourceAmount) || sourceAmount < 0) {
      setError("Số tiền nguồn không hợp lệ.");
      setSubmitting(false);
      return;
    }
    if (!Number.isFinite(targetAmount) || targetAmount < 0) {
      setError("Số tiền đích không hợp lệ.");
      setSubmitting(false);
      return;
    }
    if (!Number.isFinite(matchedAmount) || matchedAmount < 0) {
      setError("Số tiền khớp không hợp lệ.");
      setSubmitting(false);
      return;
    }
    if ((targetTypeRaw && !targetIdRaw) || (!targetTypeRaw && targetIdRaw)) {
      setError("Đích phải có đủ loại và mã, hoặc để trống cả hai.");
      setSubmitting(false);
      return;
    }

    const body = {
      sourceType: String(fd.get("sourceType") ?? "other").trim(),
      sourceId,
      targetType: targetTypeRaw || null,
      targetId: targetIdRaw || null,
      sourceAmount,
      targetAmount,
      matchedAmount,
      currencyCode: String(fd.get("currencyCode") ?? "VND")
        .trim()
        .toUpperCase(),
      notes: String(fd.get("notes") ?? "").trim() || null,
    };

    try {
      const res = await fetch(
        `/bff/reconciliations/${reconciliationId}/details`,
        {
          method: "POST",
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
        setError(
          payload.message ||
            (res.status === 409
              ? "Không thêm được dòng (trạng thái / số tiền). Tải lại trang."
              : "Thêm chi tiết đối soát thất bại.")
        );
        return;
      }

      (e.target as HTMLFormElement).reset();
      startTransition(() => router.refresh());
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      setSubmitting(false);
    }
  }

  const busy = submitting || isPending;
  const pickOptions = [...bankLines, ...payments, ...collections];

  return (
    <form className="receive-form" onSubmit={onSubmit}>
      <p className="note">
        Thêm {detailLabel.toLowerCase()}. Nếu nguồn − khớp ≠ 0 hệ thống tạo{" "}
        {varianceLabel.toLowerCase()} (không tự mở ngoại lệ). Chọn từ danh sách
        gợi ý hoặc dán GUID.{" "}
        <Link className="row-link" href="/bank-feed">
          Sao kê
        </Link>
        {" · "}
        <Link className="row-link" href="/settlements">
          Thanh toán / Thu
        </Link>
      </p>
      {pickOptions.length > 0 ? (
        <datalist id="recon-pick-ids">
          {pickOptions.map((o) => (
            <option key={o.id} value={o.id}>
              {o.label}
            </option>
          ))}
        </datalist>
      ) : null}
      <div className="form-grid">
        <div className="field">
          <label htmlFor="sourceType">Loại nguồn</label>
          <select
            id="sourceType"
            name="sourceType"
            defaultValue="bank_line"
            required
            disabled={busy}
          >
            {SOURCE_TYPES.map((t) => (
              <option key={t} value={t}>
                {objectLabel(terms, t)}
              </option>
            ))}
          </select>
        </div>
        <div className="field">
          <label htmlFor="sourceId">Mã nguồn</label>
          <input
            id="sourceId"
            name="sourceId"
            type="text"
            required
            disabled={busy}
            className="mono-id"
            placeholder="Chọn gợi ý hoặc GUID"
            list={pickOptions.length > 0 ? "recon-pick-ids" : undefined}
          />
        </div>
        <div className="field">
          <label htmlFor="sourceAmount">Số tiền nguồn</label>
          <input
            id="sourceAmount"
            name="sourceAmount"
            type="number"
            step="any"
            min="0"
            required
            disabled={busy}
          />
        </div>
        <div className="field">
          <label htmlFor="targetType">Loại đích (tuỳ chọn)</label>
          <select id="targetType" name="targetType" disabled={busy} defaultValue="">
            <option value="">— Không —</option>
            {SOURCE_TYPES.map((t) => (
              <option key={t} value={t}>
                {objectLabel(terms, t)}
              </option>
            ))}
          </select>
        </div>
        <div className="field">
          <label htmlFor="targetId">Mã đích</label>
          <input
            id="targetId"
            name="targetId"
            type="text"
            disabled={busy}
            className="mono-id"
            placeholder="Chọn gợi ý hoặc GUID"
            list={pickOptions.length > 0 ? "recon-pick-ids" : undefined}
          />
        </div>
        <div className="field">
          <label htmlFor="targetAmount">Số tiền đích</label>
          <input
            id="targetAmount"
            name="targetAmount"
            type="number"
            step="any"
            min="0"
            required
            defaultValue={0}
            disabled={busy}
          />
        </div>
        <div className="field">
          <label htmlFor="matchedAmount">Số tiền khớp</label>
          <input
            id="matchedAmount"
            name="matchedAmount"
            type="number"
            step="any"
            min="0"
            required
            disabled={busy}
          />
        </div>
        <div className="field">
          <label htmlFor="currencyCode">Tiền tệ</label>
          <input
            id="currencyCode"
            name="currencyCode"
            type="text"
            defaultValue="VND"
            maxLength={3}
            required
            disabled={busy}
          />
        </div>
        <div className="field field-span">
          <label htmlFor="notes">Ghi chú</label>
          <textarea id="notes" name="notes" rows={2} maxLength={2048} disabled={busy} />
        </div>
      </div>

      {error ? (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      ) : null}

      <div className="cta-row">
        <button type="submit" className="btn" disabled={busy}>
          {busy ? "Đang thêm…" : `Thêm ${detailLabel.toLowerCase()}`}
        </button>
      </div>
    </form>
  );
}
