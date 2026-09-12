import Link from "next/link";
import {
  adjustmentTypeLabel,
  maturityLabelKey,
  type CostAdjustmentItem,
} from "@/lib/costs-revenues";
import { formatDateTimeVi, formatMoney } from "@/lib/money";
import { term, type TerminologyMap } from "@/lib/terminology";

type Props = {
  terms: TerminologyMap;
  adjustments: CostAdjustmentItem[];
  currencyCode: string;
};

function maturityVi(terms: TerminologyMap, maturity: string): string {
  const key = maturityLabelKey(maturity);
  if (key === "EXPECTED") return term(terms, "EXPECTED", "Dự kiến");
  if (key === "CONFIRMED") return term(terms, "CONFIRMED", "Đã xác nhận");
  if (key === "ACTUAL") return term(terms, "ACTUAL", "Thực tế");
  return maturity;
}

export function AdjustmentHistoryTable({
  terms,
  adjustments,
  currencyCode,
}: Props) {
  if (adjustments.length === 0) {
    return (
      <div className="empty-state" role="status">
        Chưa có dòng điều chỉnh. Mỗi lần đổi số lớp hiện tại phải có lý do và
        lịch sử ở đây.
      </div>
    );
  }

  return (
    <div className="table-wrap">
      <table className="data-table">
        <caption className="sr-only">Lịch sử điều chỉnh</caption>
        <thead>
          <tr>
            <th scope="col">Loại</th>
            <th scope="col" className="num">
              Delta
            </th>
            <th scope="col">Lớp</th>
            <th scope="col" className="num">
              Trước → Sau
            </th>
            <th scope="col">Lý do</th>
            <th scope="col">Hiệu lực</th>
            <th scope="col">Ghi lúc</th>
          </tr>
        </thead>
        <tbody>
          {adjustments.map((a) => (
            <tr key={a.id}>
              <td>{adjustmentTypeLabel(a.adjustmentType)}</td>
              <td className={`num ${a.deltaAmount < 0 ? "neg" : ""}`}>
                {formatMoney(a.deltaAmount, a.currencyCode || currencyCode)}
              </td>
              <td>{maturityVi(terms, a.appliedToMaturity)}</td>
              <td className="num">
                {formatMoney(a.amountBefore, a.currencyCode || currencyCode)}
                {" → "}
                {formatMoney(a.amountAfter, a.currencyCode || currencyCode)}
              </td>
              <td>{a.reason}</td>
              <td>{a.effectiveDate}</td>
              <td>{formatDateTimeVi(a.createdAt)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

/** Optional back-link helper for detail pages */
export function LineDetailBackLink({
  href,
  label,
}: {
  href: string;
  label: string;
}) {
  return (
    <p className="meta-line">
      <Link className="row-link" href={href}>
        ← {label}
      </Link>
    </p>
  );
}
