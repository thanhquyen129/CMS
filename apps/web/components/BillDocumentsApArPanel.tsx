import Link from "next/link";
import { DocumentStatusTriad } from "@/components/DocumentStatusTriad";
import type {
  AccountsPayableItem,
  AccountsReceivableItem,
} from "@/lib/ap-ar";
import {
  agingBucketLabel,
  isOutstanding,
  settlementStatusLabel,
} from "@/lib/ap-ar";
import type { FinancialDocumentListItem } from "@/lib/documents";
import {
  directionLabel,
  documentTypeLabel,
} from "@/lib/documents";
import { formatMoney } from "@/lib/money";
import type { TerminologyMap } from "@/lib/terminology";
import { term } from "@/lib/terminology";

type Props = {
  terms: TerminologyMap;
  billId: string;
  documents: FinancialDocumentListItem[] | null;
  documentsError: string | null;
  payables: AccountsPayableItem[] | null;
  payablesError: string | null;
  receivables: AccountsReceivableItem[] | null;
  receivablesError: string | null;
};

/** Bill-scoped documents + AP/AR outstanding (read) + CTAs. Cost ≠ Payment. */
export function BillDocumentsApArPanel({
  terms,
  billId,
  documents,
  documentsError,
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
  const collectionLabel = term(terms, "COLLECTION", "Thu tiền");
  const closeLabel = term(terms, "FINANCIAL_CLOSE", "Chốt tài chính");
  const receivedLabel = term(terms, "RECEIVED", "Đã nhận");
  const acceptedLabel = term(terms, "ACCEPTED", "Đã chấp nhận");
  const matchedLabel = term(terms, "MATCHED", "Đã khớp");

  const apRows = (payables ?? []).filter(
    (r) => r.billId === billId && isOutstanding(r)
  );
  const arRows = (receivables ?? []).filter(
    (r) => r.billId === billId && isOutstanding(r)
  );

  const docsHref = `/documents?billId=${encodeURIComponent(billId)}`;

  return (
    <>
      <h2 className="section-title">{docLabel}</h2>
      <p className="note">
        Nhận ≠ Chấp nhận ≠ Khớp. {docLabel} ≠ {costLabel} ≠ {paymentLabel}.
        Danh sách dưới đây lọc theo Bill này (header hoặc dòng).
      </p>
      <p className="cta-row" style={{ marginTop: 0 }}>
        <Link
          className="btn btn-sm"
          href={`/documents/receive?billId=${billId}`}
        >
          Nhận {docLabel.toLowerCase()}
        </Link>{" "}
        <Link className="btn btn-ghost btn-sm" href={docsHref}>
          Danh sách theo Bill
        </Link>
      </p>

      {documentsError ? (
        <div className="alert alert-error" role="alert">
          {documentsError}
        </div>
      ) : documents === null ? null : documents.length === 0 ? (
        <div className="empty-state" role="status">
          Chưa có {docLabel.toLowerCase()} gắn Bill này. Nhận chứng từ để bắt
          đầu.
        </div>
      ) : (
        <div className="table-wrap">
          <table className="data-table">
            <thead>
              <tr>
                <th scope="col">Số</th>
                <th scope="col">Loại</th>
                <th scope="col">Chiều</th>
                <th scope="col" className="num">
                  Tổng tiền
                </th>
                <th scope="col">
                  {receivedLabel} ≠ {acceptedLabel} ≠ {matchedLabel}
                </th>
                <th scope="col">Chi tiết</th>
              </tr>
            </thead>
            <tbody>
              {documents.map((doc) => (
                <tr key={doc.id}>
                  <td>
                    <div className="queue-title">{doc.documentNo}</div>
                    <span className="muted small block">{doc.documentDate}</span>
                  </td>
                  <td>{documentTypeLabel(doc.documentType)}</td>
                  <td>{directionLabel(terms, doc.direction)}</td>
                  <td className="num">
                    {formatMoney(doc.totalAmount, doc.currencyCode)}
                  </td>
                  <td>
                    <DocumentStatusTriad
                      terms={terms}
                      receiptStatus={doc.receiptStatus}
                      acceptanceStatus={doc.acceptanceStatus}
                      matchingStatus={doc.matchingStatus}
                      compact
                    />
                  </td>
                  <td>
                    <Link className="row-link" href={`/documents/${doc.id}`}>
                      Mở
                    </Link>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <h2 className="section-title">
        {apLabel} / {arLabel} — {outstandingLabel}
      </h2>
      <p className="note">
        Số dư đã ghi nhận trên Bill này. Không phải số {costLabel}. Tất toán qua{" "}
        {paymentLabel} / {collectionLabel}.
      </p>
      <p className="cta-row" style={{ marginTop: 0 }}>
        <Link
          className="btn btn-sm"
          href={`/ap-ar/exposures/new?kind=payable&billId=${billId}`}
        >
          Tạo exposure phải trả
        </Link>{" "}
        <Link
          className="btn btn-sm"
          href={`/ap-ar/exposures/new?kind=receivable&billId=${billId}`}
        >
          Tạo exposure phải thu
        </Link>{" "}
        <Link
          className="btn btn-sm"
          href={`/settlements/payments/new?billId=${billId}`}
        >
          Tạo {paymentLabel.toLowerCase()}
        </Link>{" "}
        <Link
          className="btn btn-sm"
          href={`/settlements/collections/new?billId=${billId}`}
        >
          Tạo {collectionLabel.toLowerCase()}
        </Link>{" "}
        <Link
          className="btn btn-ghost btn-sm"
          href={`/financial-closes/new?billId=${billId}`}
        >
          {closeLabel} theo Bill
        </Link>
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
