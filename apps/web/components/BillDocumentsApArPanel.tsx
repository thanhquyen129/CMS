import Link from "next/link";
import type { TerminologyMap } from "@/lib/terminology";
import { term } from "@/lib/terminology";
import type {
  AccountsPayableItem,
  AccountsReceivableItem,
} from "@/lib/ap-ar";
import {
  agingBucketLabel,
  isOutstanding,
  settlementStatusLabel,
} from "@/lib/ap-ar";
import { formatMoney } from "@/lib/money";

type Props = {
  terms: TerminologyMap;
  billId: string;
  payables: AccountsPayableItem[] | null;
  payablesError: string | null;
  receivables: AccountsReceivableItem[] | null;
  receivablesError: string | null;
};

/** Bill-scoped AP/AR outstanding (read) + document CTAs. Cost ≠ Payment. */
export function BillDocumentsApArPanel({
  terms,
  billId,
  payables,
  payablesError,
  receivables,
  receivablesError,
}: Props) {
  const docLabel = term(terms, "FINANCIAL_DOCUMENT", "Chứng từ tài chính");
  const apLabel = term(terms, "ACCOUNTS_PAYABLE", "Khoản phải trả");
  const arLabel = term(terms, "ACCOUNTS_RECEIVABLE", "Khoản phải thu");
  const outstandingLabel = term(terms, "OUTSTANDING", "Số dư còn lại");
  const costLabel = term(terms, "COST", "Chi phí");
  const paymentLabel = term(terms, "PAYMENT", "Thanh toán");

  const apRows = (payables ?? []).filter(
    (r) => r.billId === billId && isOutstanding(r)
  );
  const arRows = (receivables ?? []).filter(
    (r) => r.billId === billId && isOutstanding(r)
  );

  return (
    <>
      <h2 className="section-title">{docLabel}</h2>
      <p className="note">
        Nhận ≠ Chấp nhận ≠ Khớp. {docLabel} ≠ {costLabel} ≠ {paymentLabel}.
        Danh sách API chưa lọc theo Bill — mở danh sách chung hoặc nhận mới gắn
        Bill này.
      </p>
      <p className="cta-row" style={{ marginTop: 0 }}>
        <Link
          className="btn btn-sm"
          href={`/documents/receive?billId=${billId}`}
        >
          Nhận {docLabel.toLowerCase()}
        </Link>{" "}
        <Link className="btn btn-ghost btn-sm" href="/documents">
          Danh sách {docLabel.toLowerCase()}
        </Link>
      </p>

      <h2 className="section-title">
        {apLabel} / {arLabel} — {outstandingLabel}
      </h2>
      <p className="note">
        Số dư đã ghi nhận trên Bill này. Không phải số {costLabel}; chưa gồm bước{" "}
        {paymentLabel}.
      </p>

      <h3 className="section-title sm">{apLabel}</h3>
      {payablesError ? (
        <div className="alert alert-error" role="alert">
          {payablesError}
        </div>
      ) : payables === null ? null : apRows.length === 0 ? (
        <div className="empty-state" role="status">
          Không có {apLabel.toLowerCase()} còn dư trên Bill này.
        </div>
      ) : (
        <div className="table-wrap">
          <table className="data-table">
            <thead>
              <tr>
                <th scope="col" className="num">
                  {outstandingLabel}
                </th>
                <th scope="col">Tất toán</th>
                <th scope="col">Tuổi nợ</th>
              </tr>
            </thead>
            <tbody>
              {apRows.map((row) => (
                <tr key={row.id}>
                  <td className="num">
                    {formatMoney(row.outstanding, row.currencyCode)}
                  </td>
                  <td>{settlementStatusLabel(terms, row.settlementStatus)}</td>
                  <td>{agingBucketLabel(row.agingBucket)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <h3 className="section-title sm">{arLabel}</h3>
      {receivablesError ? (
        <div className="alert alert-error" role="alert">
          {receivablesError}
        </div>
      ) : receivables === null ? null : arRows.length === 0 ? (
        <div className="empty-state" role="status">
          Không có {arLabel.toLowerCase()} còn dư trên Bill này.
        </div>
      ) : (
        <div className="table-wrap">
          <table className="data-table">
            <thead>
              <tr>
                <th scope="col" className="num">
                  {outstandingLabel}
                </th>
                <th scope="col">Tất toán</th>
                <th scope="col">Tuổi nợ</th>
              </tr>
            </thead>
            <tbody>
              {arRows.map((row) => (
                <tr key={row.id}>
                  <td className="num">
                    {formatMoney(row.outstanding, row.currencyCode)}
                  </td>
                  <td>{settlementStatusLabel(terms, row.settlementStatus)}</td>
                  <td>{agingBucketLabel(row.agingBucket)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </>
  );
}
