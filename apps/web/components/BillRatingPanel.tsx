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
                  Tổng
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
                    {formatMoney(r.totalAmount, r.currencyCode)}
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
                      />
                    ) : (
                      <div className="row-actions">
                        <SeedExpectedCostsButton terms={terms} ratingId={r.id} />
                        <SeedExpectedRevenuesButton
                          terms={terms}
                          ratingId={r.id}
                        />
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
  return (
    <div className="stack">
      <h3 className="section-title sm">
        {side} · v vừa tính · {formatMoney(rating.totalAmount, rating.currencyCode)}
      </h3>
      <div className="table-wrap">
        <table className="data-table">
          <thead>
            <tr>
              <th>Nguồn</th>
              <th>Công thức</th>
              <th className="num">Thành tiền</th>
              <th className="num">Nguyên tệ</th>
              <th className="num">Tỷ giá</th>
              <th className="num">VAT</th>
              <th>Độ chín</th>
            </tr>
          </thead>
          <tbody>
            {rating.details.map((d) => (
              <tr key={d.id}>
                <td>
                  <code>{d.ruleCode}</code>
                  {d.componentCode ? ` · ${d.componentCode}` : ""}
                </td>
                <td>{d.formulaText || d.componentName}</td>
                <td className="num">{formatMoney(d.amount, d.currencyCode)}</td>
                <td className="num">
                  {d.amountOriginal != null && d.originalCurrency
                    ? formatMoney(d.amountOriginal, d.originalCurrency)
                    : "—"}
                </td>
                <td className="num">{d.lineFxRate ?? "—"}</td>
                <td className="num">{d.vatRate == null ? "chưa khai báo" : `${d.vatRate}%`}</td>
                <td>{d.financialMaturity === "expected" ? "Dự kiến" : d.financialMaturity}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
