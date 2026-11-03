import Link from "next/link";
import { RateBillForm } from "@/components/RateBillForm";
import { RerateRatingButton } from "@/components/RerateRatingButton";
import { RatingSurchargeBuckets } from "@/components/RatingSurchargeBuckets";
import { ServiceSelectionPanel, type ServiceOption } from "@/components/ServiceSelectionPanel";
import { basisLabel, formatChargeable, ratingStatusView } from "@/lib/opref-edit";
import { ApplyRatingFinancialsButton } from "@/components/ApplyRatingFinancialsButton";
import { BillRatingFinancialWorkspace } from "@/components/BillRatingFinancialWorkspace";
import { formatDateTimeVi, formatMoney } from "@/lib/money";
import { partyTypeLabel, type Rating } from "@/lib/rate-cards";
import {
  getBillRatingMargin,
  getRating,
  listOptionalServiceOptions,
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

  const [ratingsRes, marginRes, optionalRes] = await Promise.all([
    fetchRatingsByBill(billId),
    getBillRatingMargin(billId),
    listOptionalServiceOptions(),
  ]);
  const optionalOptions: ServiceOption[] = optionalRes.ok
    ? optionalRes.data.map((row) => ({
        serviceCode: row.serviceCode,
        name: row.name,
        selectionScope: row.selectionScope,
        hasBuyPrice: row.hasBuyPrice,
        hasSellPrice: row.hasSellPrice,
      }))
    : [];

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
      <h2 className="section-title">Tính giá &amp; Tài chính</h2>
      {staleCount > 0 ? (
        <div className="alert alert-warning" role="status">
          <strong>Cần tính giá lại</strong> — {staleCount} lần tính giá dùng dữ liệu Bill đã thay đổi. Kết quả cũ
          được giữ nguyên; bấm “Tính giá lại” ở dòng tương ứng để tạo lần tính giá mới.
        </div>
      ) : null}
      <p className="note">
        Workspace hợp nhất: tính thử → xem trước → áp dụng vào hồ sơ tài chính (lớp {expected}). Tính thử không
        tự tạo chi phí/doanh thu. Áp dụng khử trùng theo nghĩa vụ kinh tế, không ghi đè Thực tế/Đã xác nhận.{" "}
        <Link className="row-link" href="/rate-cards">
          Quản lý bảng giá
        </Link>
      </p>

      <RateBillForm terms={terms} billId={billId} />

      <ServiceSelectionPanel contextType="BILL" contextId={billId} options={optionalOptions} />

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

      <h3 className="section-title sm">A. Ngữ cảnh tính giá</h3>
      <div
        className="panel"
        style={{
          display: "grid",
          gridTemplateColumns: "repeat(auto-fit,minmax(140px,1fr))",
          gap: "0.75rem",
          padding: "12px 14px",
          border: "1px solid #e2e8f0",
          borderRadius: 8,
          background: "#f8fafc",
          marginBottom: "1rem",
        }}
      >
        <div>
          <span className="muted small block">Bill / Shipment</span>
          <strong>{billLabel}</strong>
          <span className="muted small block">{billId.slice(0, 8)}…</span>
        </div>
        <div>
          <span className="muted small block">Chiều</span>
          <strong>
            {latestBuy ? "MUA (BUY)" : "—"}
            {latestBuy && latestSell ? " · " : ""}
            {latestSell ? "BÁN (SELL)" : ""}
          </strong>
        </div>
        <div>
          <span className="muted small block">as-of / CW</span>
          <strong>
            {latestBuy?.ratedAt || latestSell?.ratedAt
              ? formatDateTimeVi(latestBuy?.ratedAt || latestSell?.ratedAt || "")
              : "—"}
          </strong>
          <span className="muted small block">
            {formatChargeable(
              latestBuy?.chargeableWeightKg ?? latestSell?.chargeableWeightKg ?? null
            )}
          </span>
        </div>
        <div>
          <span className="muted small block">Dịch vụ / Mode</span>
          <strong>
            {breakdowns[0]?.serviceTypeCode || breakdowns[1]?.serviceTypeCode || "—"}
          </strong>
        </div>
      </div>

      <h3 className="section-title sm">B. Phương án &amp; So sánh</h3>
      <p className="note">
        Nhãn trạng thái Rating: <em>Tính thử (Chưa áp dụng)</em>, <em>Có thể áp dụng</em>, <em>Đang áp dụng</em>,{" "}
        <em>Chỉ so sánh</em>, <em>Đã thay thế (Lịch sử)</em> — không dùng «Đã xác nhận» cho Rating.
      </p>
      <div className="table-wrap" style={{ marginBottom: "1rem" }}>
        <table className="data-table">
          <thead>
            <tr>
              <th>Chiều</th>
              <th>Phương án đang áp dụng (Current Applied)</th>
              <th>Phương án tính thử (Candidate)</th>
              <th>Trạng thái Rating</th>
            </tr>
          </thead>
          <tbody>
            {[
              { label: "MUA (BUY)", current: latestBuy, candidate: latestBuy },
              { label: "BÁN (SELL)", current: latestSell, candidate: latestSell },
            ].map((row) => {
              const status = row.candidate
                ? ratingStatusView(row.candidate.status, row.candidate.staleAt)
                : { label: "Chưa có phương án", className: "muted" };
              return (
                <tr key={row.label}>
                  <td>{row.label}</td>
                  <td>
                    {row.current
                      ? formatMoney(row.current.totalAmount, row.current.currencyCode)
                      : "—"}
                  </td>
                  <td>
                    {row.candidate
                      ? formatMoney(row.candidate.totalAmount, row.candidate.currencyCode)
                      : "—"}
                  </td>
                  <td>
                    <span className={status.className}>{status.label}</span>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>

      {breakdowns.map((rating) => (
        <RatingBreakdown key={rating.id} rating={rating} />
      ))}

      <BillRatingFinancialWorkspace
        billId={billId}
        buyRatingId={latestBuy?.id}
        sellRatingId={latestSell?.id}
      />

      <h3 className="section-title sm">Lịch sử tính giá (chi tiết)</h3>
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
                      <ApplyRatingFinancialsButton
                        ratingId={r.id}
                        directionLabel={
                          r.partyTypeCode?.toLowerCase() === "customer" || r.partyTypeCode?.toLowerCase() === "sell"
                            ? "BÁN (SELL)"
                            : "MUA (BUY)"
                        }
                      />
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
        <RatingSurchargeBuckets contextJson={rating.contextJson} currencyCode={rating.currencyCode} />
      </div>
    </div>
  );
}
