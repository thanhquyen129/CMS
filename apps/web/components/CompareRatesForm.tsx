"use client";

import type { FormEvent } from "react";
import { useState } from "react";
import { formatMoney } from "@/lib/money";
import { partyTypeLabel } from "@/lib/rate-cards";

export type RateQuote = {
  rateCardId: string;
  cardCode: string;
  cardName: string;
  carrierName: string | null;
  partyType: string;
  versionNo: number;
  currencyCode: string;
  totalAmount: number;
  baseAmount: number;
  surchargeAmount: number;
  ruleCodes: string;
  error: string | null;
};

export function CompareRatesForm({ initial }: { initial: Record<string, string> }) {
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [quotes, setQuotes] = useState<RateQuote[] | null>(null);

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    const fd = new FormData(e.currentTarget);
    const quantity = Number(String(fd.get("quantity") ?? "").replace(",", "."));
    const body = {
      transportMode: String(fd.get("transportMode") ?? ""),
      originCode: String(fd.get("originCode") ?? "").trim() || null,
      destinationCode: String(fd.get("destinationCode") ?? "").trim() || null,
      rateDate: String(fd.get("rateDate") ?? "") || null,
      quantity: Number.isFinite(quantity) && quantity > 0 ? quantity : null,
      partyType: String(fd.get("partyType") ?? "") || null,
    };
    try {
      const res = await fetch("/bff/ratings/compare", {
        method: "POST",
        headers: { "Content-Type": "application/json", Accept: "application/json" },
        body: JSON.stringify(body),
      });
      const data = (await res.json().catch(() => null)) as RateQuote[] | { message?: string } | null;
      if (!res.ok || !Array.isArray(data)) {
        setError((data && "message" in data && data.message) || "Không so sánh được giá.");
        setQuotes(null);
        return;
      }
      setQuotes(data);
    } catch {
      setError("Không kết nối được máy chủ.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <>
      <form className="form-grid" onSubmit={onSubmit}>
        <div className="field">
          <label htmlFor="transportMode">Phương thức</label>
          <select id="transportMode" name="transportMode" defaultValue={initial.transportMode || "air"}>
            <option value="air">Air</option>
            <option value="sea">Sea</option>
            <option value="road">Road</option>
          </select>
        </div>
        <div className="field">
          <label htmlFor="originCode">Từ</label>
          <input id="originCode" name="originCode" defaultValue={initial.originCode || ""} placeholder="SGN" />
        </div>
        <div className="field">
          <label htmlFor="destinationCode">Đến</label>
          <input id="destinationCode" name="destinationCode" defaultValue={initial.destinationCode || ""} placeholder="FRA" />
        </div>
        <div className="field">
          <label htmlFor="rateDate">Ngày đi</label>
          <input id="rateDate" name="rateDate" type="date" defaultValue={initial.rateDate || ""} />
        </div>
        <div className="field">
          <label htmlFor="quantity">Chargeable Weight</label>
          <input id="quantity" name="quantity" inputMode="decimal" defaultValue={initial.quantity || ""} placeholder="100" />
        </div>
        <div className="field">
          <label htmlFor="partyType">Loại giá</label>
          <select id="partyType" name="partyType" defaultValue={initial.partyType || "vendor"}>
            <option value="vendor">Giá mua</option>
            <option value="customer">Giá bán</option>
          </select>
        </div>
        <div className="field" style={{ alignSelf: "end" }}>
          <button className="btn" type="submit" disabled={busy}>{busy ? "Đang so sánh…" : "So sánh"}</button>
        </div>
      </form>
      {error ? <div className="alert alert-error" role="alert">{error}</div> : null}
      {quotes && quotes.length === 0 ? <p className="empty-state">Không có bảng giá phù hợp đang hiệu lực.</p> : null}
      {quotes && quotes.length > 0 ? (
        <>
          <div className="stat-grid">
            {quotes.map((q) => (
              <article key={q.rateCardId + q.versionNo} className="stat-card">
                <p>{partyTypeLabel(q.partyType)} · {q.carrierName || q.cardName}</p>
                <p><code>{q.cardCode}</code> · v{q.versionNo}</p>
                <strong>{q.error ? "—" : formatMoney(q.totalAmount, q.currencyCode)}</strong>
                {q.error ? <p className="alert alert-error">{q.error}</p> : (
                  <p>Base {formatMoney(q.baseAmount, q.currencyCode)} · Phụ phí {formatMoney(q.surchargeAmount, q.currencyCode)}</p>
                )}
                <p className="muted">{q.ruleCodes}</p>
              </article>
            ))}
          </div>
          <table className="data-table">
            <thead>
              <tr>
                <th>Tiêu chí</th>
                {quotes.map((q) => <th key={q.cardCode}>{q.carrierName || q.cardName}</th>)}
              </tr>
            </thead>
            <tbody>
              <tr>
                <td>Tổng giá</td>
                {quotes.map((q) => <td key={q.cardCode}>{q.error ? q.error : formatMoney(q.totalAmount, q.currencyCode)}</td>)}
              </tr>
              <tr>
                <td>Rule</td>
                {quotes.map((q) => <td key={`${q.cardCode}-r`}>{q.ruleCodes || "—"}</td>)}
              </tr>
            </tbody>
          </table>
          <p className="muted">So sánh không ghi Rating. Chọn bảng giá rồi tính trên Bill để lưu snapshot.</p>
        </>
      ) : null}
    </>
  );
}
