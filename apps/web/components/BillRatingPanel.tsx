import Link from "next/link";
import { RateBillForm } from "@/components/RateBillForm";
import { RerateRatingButton } from "@/components/RerateRatingButton";
import { basisLabel, formatChargeable, ratingStatusView } from "@/lib/opref-edit";
import { SeedExpectedCostsButton } from "@/components/SeedExpectedCostsButton";
import { SeedExpectedRevenuesButton } from "@/components/SeedExpectedRevenuesButton";
import { formatDateTimeVi, formatMoney } from "@/lib/money";
import { partyTypeLabel, type Rating } from "@/lib/rate-cards";
import {
  getBillRatingMargin,
  getRating,
  listRatingsByBill as fetchRatingsByBill,
} from "@/lib/rate-cards-server";
import { term, type TerminologyMap } from "@/lib/terminology";

type Props = {
  terms: TerminologyMap;
  billId: string;
};

export async function BillRatingPanel({ terms, billId }: Props) {
  const billLabel = term(terms, "BILL", "Bill");
  const expected = term(terms, "EXPECTED", "Dự kiến");

  const [ratingsRes, marginRes] = await Promise.all([
    fetchRatingsByBill(billId),
    getBillRatingMargin(billId),
  ]);

  const ratings = ratingsRes.ok ? ratingsRes.data : [];
  const activeRatings = ratings.filter((r) => r.status?.toLowerCase() !== "superseded");
  const latestBuy = activeRatings.find((r) => r.partyTypeCode?.toLowerCase() === "vendor");
  const latestSell = activeRatings.find((r) => r.partyTypeCode?.toLowerCase() === "customer");
  const breakdownIds = [latestBuy?.id, latestSell?.id].filter((id): id is string => Boolean(id));
  const breakdowns = (
    await Promise.all(breakdownIds.map(async (id) => getRating(id)))
  ).flatMap((res) => (res.ok ? [res.data] : []));
  const margin = marginRes.ok ? marginRes.data : null;

  const staleCount = ratings.filter(
    (r) => r.staleAt && r.status?.toLowerCase() !== "superseded"
  ).length;

  return (
    <div className="stack" style={{ marginTop: "1.5rem" }} id="bill-rating">
      <h2 className="section-title">Tính giá / Rating</h2>
      {staleCount > 0 ? (
        <div className="alert alert-warning" role="status">
          <strong>Cần tính giá lại</strong> — {staleCount} lần tính giá dùng dữ liệu Bill đã thay đổi. Kết quả cũ
          được giữ nguyên; bấm “Tính giá lại” ở dòng tương ứng để tạo lần tính giá mới.
        </div>
      ) : null}
      <p className="note">
        Áp bảng giá đã phát hành lên {billLabel} này.         Seed tạo chi phí / doanh thu lớp {expected} từ thành phần bảng giá
        (không ghi đè dòng đã seed).{" "}
        <Link className="row-link" href="/rate-cards">
          Quản lý bảng giá
        </Link>
      </p>

      <RateBillForm terms={terms} billId={billId} />

      <h3 className="section-title sm">Lợi nhuận dự kiến</h3>
      {!margin ? (
        <p className="note">{marginRes.ok ? "" : marginRes.message}</p>
      ) : margin.fxMissing ? (
        <div className="alert alert-warning" role="status">
          {margin.notice}
        </div>
      ) : margin.reportingProfit == null ? (
        <p className="note">{margin.notice}</p>
      ) : (
        <p>
          {margin.notice}{" "}
          <strong>{formatMoney(margin.reportingProfit, margin.reportingCurrency)}</strong>
        </p>
      )}

      {breakdowns.map((rating) => (
        <RatingBreakdown key={rating.id} rating={rating} />
      ))}

      <h3 className="section-title sm">Lịch sử tính giá</h3>
      {!ratingsRes.ok ? (
        <div className="alert alert-error" role="alert">
          {ratingsRes.message}
        </div>
      ) : ratings.length === 0 ? (
        <div className="empty-state" role="status">
          Chưa có lần tính giá trên {billLabel} này.
        </div>
      ) : (
        <div className="table-wrap">
          <table className="data-table">
            <thead>
              <tr>
                <th scope="col">Thời điểm</th>
                <th scope="col">Chiều</th>
                <th scope="col" className="num">
                  Tổng Net (Trước thuế)
                </th>
                <th scope="col" className="num">
                  Tổng Gross (Sau thuế)
                </th>
                <th scope="col" className="num">
                  TL tính cước
                </th>
                <th scope="col">Trạng thái</th>
                <th scope="col">Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {ratings.map((r) => {
                const status = ratingStatusView(r.status, r.staleAt);
                const superseded = r.status?.toLowerCase() === "superseded";
                return (
                <tr key={r.id}>
                  <td>{formatDateTimeVi(r.ratedAt)}</td>
                  <td>{r.partyTypeCode ? partyTypeLabel(r.partyTypeCode) : "—"}</td>
                  <td className="num">
                    <strong>{formatMoney(r.totalAmount, r.currencyCode)}</strong>
                  </td>
                  <td className="num">
                    <strong>{formatMoney(Math.round(r.totalAmount * 1.08), r.currencyCode)}</strong>
                    <span className="muted small block">(VAT 8%)</span>
                  </td>
                  <td className="num">
                    {r.chargeableBasis === "not_required"
                      ? "Không cần"
                      : formatChargeable(r.chargeableWeightKg ?? null)}
                    <span className="muted small block">{basisLabel(r.chargeableBasis)}</span>
                  </td>
                  <td>
                    <span className={status.className}>{status.label}</span>
                    {r.staleAt && !superseded && r.staleReason ? (
                      <span className="muted small block">{r.staleReason}</span>
                    ) : null}
                  </td>
                  <td>
                    {superseded ? (
                      <span className="muted">—</span>
                    ) : r.staleAt ? (
                      <RerateRatingButton
                        billId={billId}
                        ratingId={r.id}
                        rateVersionId={r.rateVersionId}
                        contextType={r.contextType}
                        transportLegId={r.transportLegId}
                        vendorPartyId={r.vendorPartyId}
                      />
                    ) : (
                      <div className="row-actions">
                        {(!r.partyTypeCode || r.partyTypeCode.toLowerCase() === "vendor" || r.partyTypeCode.toLowerCase() === "buy") ? (
                          <SeedExpectedCostsButton terms={terms} ratingId={r.id} billId={billId} />
                        ) : null}
                        {(r.partyTypeCode?.toLowerCase() === "customer" || r.partyTypeCode?.toLowerCase() === "sell") ? (
                          <SeedExpectedRevenuesButton terms={terms} ratingId={r.id} billId={billId} />
                        ) : null}
                      </div>
                    )}
                  </td>
                </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

function RatingBreakdown({ rating }: { rating: Rating }) {
  const side = rating.partyTypeCode ? partyTypeLabel(rating.partyTypeCode) : "Kết quả";
  const totalNet = rating.details.reduce((sum, d) => sum + (d.amount || 0), 0) || rating.totalAmount;
  const totalVat = rating.details.reduce((sum, d) => {
    const net = d.amount || 0;
    return sum + (d.vatAmount != null ? d.vatAmount : (d.vatRate != null ? Math.round(net * (d.vatRate / 100) * 100) / 100 : 0));
  }, 0);
  const totalGross = totalNet + totalVat;

  return (
    <div className="stack" style={{ marginTop: "1rem", marginBottom: "1.5rem" }}>
      <div className="panel" style={{ background: "#f8fafc", border: "1px solid #e2e8f0", borderRadius: "8px", padding: "16px" }}>
        <h3 className="section-title sm" style={{ marginBottom: "8px" }}>
          {side} · v vừa tính · {rating.serviceTypeCode ? `Dịch vụ: ${rating.serviceTypeCode} · ` : ""}Bóc tách Thuế &amp; Tiền (VAT Disclosure)
        </h3>
        <div style={{ display: "flex", gap: "24px", flexWrap: "wrap", marginBottom: "16px", padding: "12px", background: "#ffffff", borderRadius: "6px", border: "1px solid #cbd5e1" }}>
          <div>
            <span className="muted small block" style={{ fontWeight: 600 }}>Tiền trước thuế (Net):</span>
            <strong style={{ fontSize: "1.15rem", color: "#0f172a" }}>{formatMoney(totalNet, rating.currencyCode)}</strong>
          </div>
          <div>
            <span className="muted small block" style={{ fontWeight: 600 }}>Tiền thuế VAT (8%):</span>
            <strong style={{ fontSize: "1.15rem", color: "#b45309" }}>{formatMoney(totalVat, rating.currencyCode)}</strong>
          </div>
          <div>
            <span className="muted small block" style={{ fontWeight: 600 }}>Tổng tiền sau thuế (Gross):</span>
            <strong style={{ fontSize: "1.15rem", color: "#1d4e89" }}>{formatMoney(totalGross, rating.currencyCode)}</strong>
          </div>
        </div>
        <div className="table-wrap">
          <table className="data-table">
            <thead>
              <tr>
                <th>Nguồn / Mã quy tắc</th>
                <th>Công thức tính</th>
                <th className="num">Tiền trước thuế (Net)</th>
                <th className="num">Thuế suất VAT</th>
                <th className="num">Tiền thuế VAT</th>
                <th className="num">Tổng sau thuế (Gross)</th>
                <th>Độ chín</th>
              </tr>
            </thead>
            <tbody>
              {rating.details.map((d) => {
                const net = d.amount || 0;
                const vatRate = d.vatRate;
                const vatAmt = d.vatAmount != null ? d.vatAmount : (vatRate != null ? Math.round(net * (vatRate / 100) * 100) / 100 : 0);
                const gross = net + vatAmt;
                return (
                  <tr key={d.id}>
                    <td>
                      <code>{d.ruleCode}</code>
                      {d.componentCode ? ` · ${d.componentCode}` : ""}
                    </td>
                    <td>{d.formulaText || d.componentName}</td>
                    <td className="num"><strong>{formatMoney(net, d.currencyCode)}</strong></td>
                    <td className="num">{vatRate == null ? "Chưa khai báo" : `${vatRate}%`}</td>
                    <td className="num">{formatMoney(vatAmt, d.currencyCode)}</td>
                    <td className="num"><strong>{formatMoney(gross, d.currencyCode)}</strong></td>
                    <td><span className="badge badge-subtle">{d.financialMaturity === "expected" ? "Dự kiến" : d.financialMaturity}</span></td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
