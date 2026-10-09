import Link from "next/link";
import type { ChargeProfitability, ChargeProfitSource } from "@/lib/bills";
import { formatMoney } from "@/lib/money";

function sourceHref(source: ChargeProfitSource): string {
  if (source.sourceKind === "revenue") return `/revenues/${source.sourceId}`;
  if (source.sourceKind === "allocation") return `/costs/shared/${source.sourceId}`;
  return `/costs/${source.sourceId}`;
}

function sourceLabel(source: ChargeProfitSource): string {
  const side = source.side === "cost" ? "Chi phí" : "Doanh thu";
  const kind =
    source.sourceKind === "allocation"
      ? "phân bổ"
      : source.sourceKind === "revenue"
        ? "doanh thu"
        : "chi phí";
  return `${side} ${kind}`;
}

export function ChargeProfitTable({ data }: { data: ChargeProfitability }) {
  if (data.rows.length === 0) return null;
  const currency = data.reportingCurrency || "VND";
  return (
    <div className="table-wrap">
      <h3 className="section-title sm">Lợi nhuận theo khoản mục</h3>
      <p className="muted small">
        Tiền trước VAT, quy về {data.reportingCurrency || "tiền báo cáo"}. {data.view}
      </p>
      <table className="data-table">
        <thead>
          <tr>
            <th scope="col">Khoản mục</th>
            <th scope="col" className="num">Giá mua</th>
            <th scope="col" className="num">Giá bán</th>
            <th scope="col" className="num">Lợi nhuận</th>
            <th scope="col">Trạng thái</th>
          </tr>
        </thead>
        <tbody>
          {data.rows.map((row) => (
            <tr key={`${row.economicChargeTypeId ?? "x"}-${row.chargeCode}`}>
              <td>
                {row.chargeName}
                <div className="muted small">
                  {row.sources.length === 0
                    ? "Chưa có dòng"
                    : row.sources.map((source, index) => (
                        <span key={`${source.side}-${source.sourceKind}-${source.sourceId}-${source.billId ?? ""}`}>
                          {index > 0 ? " · " : null}
                          <Link className="row-link" href={sourceHref(source)}>
                            {sourceLabel(source)}
                          </Link>
                        </span>
                      ))}
                </div>
              </td>
              <td className="num">
                {row.costReporting == null ? "—" : formatMoney(row.costReporting, currency)}
              </td>
              <td className="num">
                {row.revenueReporting == null ? "—" : formatMoney(row.revenueReporting, currency)}
              </td>
              <td className={`num ${row.negativeFlag ? "neg" : ""}`}>
                {row.profitReporting == null ? "—" : formatMoney(row.profitReporting, currency)}
              </td>
              <td>{row.negativeFlag ? "Cảnh báo bán lỗ" : row.note}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
