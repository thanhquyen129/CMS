"use client";

import type { FormEvent } from "react";
import Link from "next/link";
import { useState } from "react";
import { formatMoney } from "@/lib/money";

type Option = { id: string; label: string };

type Detail = {
  componentName: string;
  formulaText?: string | null;
  amount: number;
  currencyCode: string;
};

type RatingResult = {
  totalAmount: number;
  currencyCode: string;
  chargeableBasis?: string | null;
  chargeableWeightKg?: number | null;
  originalAmount?: number | null;
  fxRate?: number | null;
  fxSource?: string | null;
  roundedAmount?: number | null;
  details: Detail[];
};

export function RateCalculatorForm({
  bills,
  versions,
}: {
  bills: Option[];
  versions: Option[];
}) {
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [result, setResult] = useState<RatingResult | null>(null);

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setError(null);
    setResult(null);
    setBusy(true);
    const fd = new FormData(e.currentTarget);
    const quantity = Number(String(fd.get("chargeable") || fd.get("gross") || ""));
    const body = {
      billId: String(fd.get("billId") ?? ""),
      rateVersionId: String(fd.get("rateVersionId") ?? ""),
      transportMode: String(fd.get("transportMode") ?? ""),
      partyTypeCode: String(fd.get("partyType") ?? ""),
      rateDate: String(fd.get("rateDate") ?? "") || null,
      originCode: String(fd.get("originCode") ?? "").trim() || null,
      destinationCode: String(fd.get("destinationCode") ?? "").trim() || null,
      commodityCode: String(fd.get("commodityCode") ?? "").trim() || null,
      grossWeightKg: num(fd.get("gross")),
      quantity: Number.isFinite(quantity) && quantity > 0 ? quantity : null,
      volumeCbm: num(fd.get("volume")),
      chargeableOverrideReason: String(fd.get("reason") ?? "").trim() || null,
    };
    try {
      const res = await fetch("/bff/ratings", {
        method: "POST",
        headers: { "Content-Type": "application/json", Accept: "application/json" },
        body: JSON.stringify(body),
      });
      const created = (await res.json().catch(() => ({}))) as { id?: string; message?: string };
      if (!res.ok || !created.id) {
        setError(created.message || "Không tính được giá.");
        return;
      }
      const got = await fetch(`/bff/ratings/${created.id}`, { headers: { Accept: "application/json" } });
      const rating = (await got.json()) as RatingResult;
      if (!got.ok) {
        setError("Đã tính giá nhưng không tải được kết quả.");
        return;
      }
      setResult(rating);
    } catch {
      setError("Không kết nối được máy chủ.");
    } finally {
      setBusy(false);
    }
  }

  const base = result?.details?.[0]?.amount ?? result?.totalAmount ?? 0;

  return (
    <div className="layout-cols-2">
      <form onSubmit={onSubmit}>
        <fieldset className="group-box">
          <legend>Thông tin yêu cầu tính giá</legend>
          <div className="form-grid">
            <div className="field">
              <label htmlFor="billId">Đối tượng tính giá <span className="req">*</span></label>
              <select id="billId" name="billId" required defaultValue="">
                <option value="" disabled>Bill</option>
                {bills.map((b) => (
                  <option key={b.id} value={b.id}>{b.label}</option>
                ))}
              </select>
            </div>
            <div className="field">
              <label htmlFor="rateVersionId">Tham chiếu nghiệp vụ <span className="req">*</span></label>
              <select id="rateVersionId" name="rateVersionId" required defaultValue="">
                <option value="" disabled>Chọn phiên bản đã phát hành</option>
                {versions.map((v) => (
                  <option key={v.id} value={v.id}>{v.label}</option>
                ))}
              </select>
            </div>
            <div className="field">
              <label htmlFor="transportMode">Phương thức <span className="req">*</span></label>
              <select id="transportMode" name="transportMode" defaultValue="air">
                <option value="air">Air</option>
                <option value="sea">Sea</option>
                <option value="road">Road</option>
              </select>
            </div>
            <div className="field">
              <label htmlFor="partyType">Loại giá <span className="req">*</span></label>
              <select id="partyType" name="partyType" defaultValue="vendor">
                <option value="vendor">Giá mua</option>
                <option value="customer">Giá bán</option>
              </select>
            </div>
            <div className="field">
              <label htmlFor="rateDate">Ngày áp dụng <span className="req">*</span></label>
              <input id="rateDate" name="rateDate" type="date" required />
            </div>
            <div className="field">
              <label htmlFor="originCode">Điểm đi</label>
              <input id="originCode" name="originCode" placeholder="SGN" />
            </div>
            <div className="field">
              <label htmlFor="destinationCode">Điểm đến</label>
              <input id="destinationCode" name="destinationCode" placeholder="FRA" />
            </div>
            <div className="field">
              <label htmlFor="commodityCode">Loại hàng</label>
              <input id="commodityCode" name="commodityCode" placeholder="Hàng thường" />
            </div>
            <div className="field">
              <label htmlFor="gross">Trọng lượng thực</label>
              <input id="gross" name="gross" inputMode="decimal" placeholder="85" />
            </div>
            <div className="field">
              <label htmlFor="chargeable">Trọng lượng tính cước</label>
              <input id="chargeable" name="chargeable" inputMode="decimal" placeholder="100" />
            </div>
            <div className="field">
              <label htmlFor="volume">Thể tích</label>
              <input id="volume" name="volume" inputMode="decimal" placeholder="0.68" />
            </div>
            <div className="field field-span">
              <label htmlFor="reason">Lý do ghi đè trọng lượng tính cước</label>
              <input id="reason" name="reason" maxLength={256} />
            </div>
          </div>
          <p className="muted">
            Rating Context được lắp từ Operational Reference và dữ liệu đo lường. Snapshot lưu bất biến cùng kết quả Rating.
          </p>
          {error ? <div className="alert alert-error" role="alert">{error}</div> : null}
          <div className="cta-row">
            <button className="btn" type="submit" disabled={busy}>{busy ? "Đang tính…" : "Tính giá"}</button>
          </div>
        </fieldset>
      </form>
      <aside className="group-box">
        <h2>Kết quả Rating</h2>
        {!result ? <p className="muted">Chưa có kết quả.</p> : (
          <>
            <dl className="metric-grid">
              <div><dt>Giá cơ bản</dt><dd>{formatMoney(base, result.currencyCode)}</dd></div>
              <div><dt>Tổng sau phụ phí</dt><dd>{formatMoney(result.totalAmount, result.currencyCode)}</dd></div>
              <div><dt>Cơ sở tính cước</dt><dd>{result.chargeableBasis || "—"} {result.chargeableWeightKg ?? ""}</dd></div>
              <div><dt>Tỷ giá</dt><dd>{result.fxSource || "—"} {result.fxRate ?? ""}</dd></div>
            </dl>
            <table className="data-table">
              <thead>
                <tr><th>Thành phần</th><th>Công thức</th><th>Thành tiền</th></tr>
              </thead>
              <tbody>
                {result.details.map((d, i) => (
                  <tr key={`${d.componentName}-${i}`}>
                    <td>{d.componentName}</td>
                    <td>{d.formulaText || "—"}</td>
                    <td>{formatMoney(d.amount, d.currencyCode)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
            <p className="notice">
              Kết quả Rating tạo Expected Cost nếu dùng bảng giá mua, hoặc Expected Revenue nếu dùng bảng giá bán. Không ghi đè Actual/Confirmed.
            </p>
          </>
        )}
        <p><Link href="/rate-cards/history">Lịch sử tính giá</Link></p>
      </aside>
    </div>
  );
}

function num(value: FormDataEntryValue | null): number | null {
  const n = Number(String(value ?? "").replace(",", "."));
  return Number.isFinite(n) && n > 0 ? n : null;
}
