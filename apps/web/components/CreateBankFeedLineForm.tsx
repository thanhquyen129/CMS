"use client";

import type { FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { term, type TerminologyMap } from "@/lib/terminology";

type Props = {
  terms: TerminologyMap;
};

export function CreateBankFeedLineForm({ terms }: Props) {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [isPending, startTransition] = useTransition();

  const lineLabel = term(terms, "BANK_FEED_LINE", "Dòng sao kê");
  const creditLabel = term(terms, "BANK_CREDIT", "Thu vào");
  const debitLabel = term(terms, "BANK_DEBIT", "Chi ra");

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setError(null);
    setSubmitting(true);

    const fd = new FormData(e.currentTarget);
    const amount = Number(String(fd.get("amount") ?? "").replace(",", "."));
    const valueDate = String(fd.get("valueDate") ?? "").trim();

    if (!valueDate) {
      setError("Chọn ngày giá trị.");
      setSubmitting(false);
      return;
    }
    if (!Number.isFinite(amount) || amount <= 0) {
      setError("Số tiền phải lớn hơn 0.");
      setSubmitting(false);
      return;
    }

    const body = {
      valueDate,
      amount,
      currencyCode: String(fd.get("currencyCode") ?? "VND")
        .trim()
        .toUpperCase(),
      direction: String(fd.get("direction") ?? "credit").trim(),
      bankReference: String(fd.get("bankReference") ?? "").trim() || null,
      counterpartyName: String(fd.get("counterpartyName") ?? "").trim() || null,
      description: String(fd.get("description") ?? "").trim() || null,
    };

    try {
      const res = await fetch("/bff/bank-feed/lines", {
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
              ? `Bạn không có quyền tạo ${lineLabel.toLowerCase()}.`
              : `Tạo ${lineLabel.toLowerCase()} thất bại.`)
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
  const today = new Date().toISOString().slice(0, 10);

  return (
    <form className="receive-form" onSubmit={onSubmit}>
      <p className="note">
        Nhập tay một dòng sao kê. Đây chưa phải thanh toán/thu tiền — đối soát
        qua phiên đối soát (nguồn = dòng sao kê).
      </p>
      <div className="form-grid">
        <div className="field">
          <label htmlFor="valueDate">Ngày giá trị</label>
          <input
            id="valueDate"
            name="valueDate"
            type="date"
            required
            defaultValue={today}
            disabled={busy}
          />
        </div>
        <div className="field">
          <label htmlFor="direction">Chiều</label>
          <select
            id="direction"
            name="direction"
            defaultValue="credit"
            required
            disabled={busy}
          >
            <option value="credit">{creditLabel}</option>
            <option value="debit">{debitLabel}</option>
          </select>
        </div>
        <div className="field">
          <label htmlFor="amount">Số tiền</label>
          <input
            id="amount"
            name="amount"
            type="number"
            step="any"
            min="0.01"
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
        <div className="field">
          <label htmlFor="bankReference">Số tham chiếu NH</label>
          <input
            id="bankReference"
            name="bankReference"
            type="text"
            maxLength={128}
            disabled={busy}
          />
        </div>
        <div className="field">
          <label htmlFor="counterpartyName">Đối tác (sao kê)</label>
          <input
            id="counterpartyName"
            name="counterpartyName"
            type="text"
            maxLength={256}
            disabled={busy}
          />
        </div>
        <div className="field field-span">
          <label htmlFor="description">Diễn giải</label>
          <textarea
            id="description"
            name="description"
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
          {busy ? "Đang lưu…" : `Thêm ${lineLabel.toLowerCase()}`}
        </button>
      </div>
    </form>
  );
}
