"use client";

import type { FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { partyLabel, type BusinessParty } from "@/lib/party";
import { term, type TerminologyMap } from "@/lib/terminology";

type Props = {
  terms: TerminologyMap;
  defaultBillId?: string;
  parties: BusinessParty[];
  partiesError?: string | null;
};

export function ReceiveDocumentForm({
  terms,
  defaultBillId,
  parties,
  partiesError,
}: Props) {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [isPending, startTransition] = useTransition();

  const docLabel = term(terms, "FINANCIAL_DOCUMENT", "Chứng từ tài chính");
  const receivedLabel = term(terms, "RECEIVED", "Đã nhận");
  const billLabel = term(terms, "BILL", "Bill");
  const paymentLabel = term(terms, "PAYMENT", "Thanh toán");
  const costLabel = term(terms, "COST", "Chi phí");

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setError(null);
    setSubmitting(true);

    const fd = new FormData(e.currentTarget);
    const totalRaw = String(fd.get("totalAmount") ?? "").trim();
    const totalAmount = Number(totalRaw.replace(",", "."));
    if (!Number.isFinite(totalAmount) || totalAmount < 0) {
      setError("Tổng tiền không hợp lệ.");
      setSubmitting(false);
      return;
    }

    const billIdRaw = String(fd.get("billId") ?? "").trim();
    const notesRaw = String(fd.get("notes") ?? "").trim();
    const dateRaw = String(fd.get("documentDate") ?? "").trim();
    const partyRaw = String(fd.get("counterpartyId") ?? "").trim();

    const body = {
      documentType: String(fd.get("documentType") ?? "").trim(),
      documentNo: String(fd.get("documentNo") ?? "").trim(),
      direction: String(fd.get("direction") ?? "").trim(),
      totalAmount,
      currencyCode: String(fd.get("currencyCode") ?? "VND")
        .trim()
        .toUpperCase(),
      documentDate: dateRaw || null,
      billId: billIdRaw || null,
      counterpartyId: partyRaw || null,
      notes: notesRaw || null,
    };

    try {
      const res = await fetch("/bff/financial-documents", {
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
              ? "Bạn không có quyền nhận chứng từ."
              : res.status === 409
                ? "Chứng từ trùng hoặc xung đột. Kiểm tra số chứng từ / đối tác."
                : "Nhận chứng từ thất bại.")
        );
        return;
      }

      const created = (await res.json().catch(() => ({}))) as { id?: string };
      if (created.id) {
        startTransition(() => router.push(`/documents/${created.id}`));
      } else {
        startTransition(() => router.push("/documents"));
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
        Nhận chỉ đặt trạng thái <strong>{receivedLabel}</strong>. Không đồng nghĩa
        chấp nhận / khớp; không tạo {costLabel} hay {paymentLabel}.
      </p>

      {error ? (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      ) : null}

      <div className="form-sections cols-2">
        <fieldset className="group-box">
          <legend>Chứng từ</legend>
          <div className="form-grid">
            <div className="field">
              <label htmlFor="documentType">Loại {docLabel.toLowerCase()}</label>
              <select id="documentType" name="documentType" required disabled={busy}>
                <option value="invoice">Hóa đơn</option>
                <option value="dn">DN</option>
                <option value="credit_note">Credit note</option>
                <option value="debit_note">Debit note</option>
                <option value="other">Khác</option>
              </select>
            </div>

            <div className="field">
              <label htmlFor="documentNo">Số chứng từ</label>
              <input
                id="documentNo"
                name="documentNo"
                required
                maxLength={128}
                disabled={busy}
                autoComplete="off"
              />
            </div>

            <div className="field">
              <label htmlFor="direction">Chiều</label>
              <select id="direction" name="direction" required disabled={busy}>
                <option value="payable">
                  {term(terms, "ACCOUNTS_PAYABLE", "Khoản phải trả")}
                </option>
                <option value="receivable">
                  {term(terms, "ACCOUNTS_RECEIVABLE", "Khoản phải thu")}
                </option>
              </select>
            </div>

            <div className="field">
              <label htmlFor="documentDate">Ngày chứng từ</label>
              <input
                id="documentDate"
                name="documentDate"
                type="date"
                disabled={busy}
              />
            </div>
          </div>
        </fieldset>

        <fieldset className="group-box">
          <legend>Số tiền &amp; liên kết</legend>
          <div className="form-grid">
            <div className="field">
              <label htmlFor="totalAmount">Tổng tiền</label>
              <input
                id="totalAmount"
                name="totalAmount"
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
                defaultValue="VND"
                maxLength={3}
                required
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
                placeholder="Để trống nếu chưa gắn Bill"
                autoComplete="off"
              />
            </div>

            <div className="field">
              <label htmlFor="counterpartyId">Đối tác (tuỳ chọn)</label>
              {partiesError ? (
                <p className="muted small" role="alert">
                  {partiesError} — vẫn nhận được không chọn đối tác.
                </p>
              ) : null}
              <select id="counterpartyId" name="counterpartyId" disabled={busy}>
                <option value="">— Không chọn —</option>
                {parties
                  .filter((p) => p.isActive)
                  .map((p) => (
                    <option key={p.id} value={p.id}>
                      {partyLabel(p)}
                    </option>
                  ))}
              </select>
            </div>

            <div className="field field-span">
              <label htmlFor="notes">Ghi chú</label>
              <input id="notes" name="notes" maxLength={2048} disabled={busy} />
            </div>
          </div>
        </fieldset>
      </div>

      <div className="cta-row">
        <button type="submit" className="btn" disabled={busy}>
          {busy ? "Đang nhận…" : `Nhận ${docLabel.toLowerCase()}`}
        </button>
      </div>
    </form>
  );
}
