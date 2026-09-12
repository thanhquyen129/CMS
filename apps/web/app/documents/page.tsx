import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { DocumentStatusTriad } from "@/components/DocumentStatusTriad";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import {
  directionLabel,
  documentTypeLabel,
  listFinancialDocuments,
} from "@/lib/documents";
import { formatMoney } from "@/lib/money";

type SearchParams = Promise<{
  receiptStatus?: string;
  acceptanceStatus?: string;
  matchingStatus?: string;
}>;

export default async function DocumentsPage({
  searchParams,
}: {
  searchParams: SearchParams;
}) {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const sp = await searchParams;
  const terms = await fetchTerminology();
  const docLabel = term(terms, "FINANCIAL_DOCUMENT", "Chứng từ tài chính");
  const receivedLabel = term(terms, "RECEIVED", "Đã nhận");
  const acceptedLabel = term(terms, "ACCEPTED", "Đã chấp nhận");
  const matchedLabel = term(terms, "MATCHED", "Đã khớp");

  const result = await listFinancialDocuments({
    receiptStatus: sp.receiptStatus,
    acceptanceStatus: sp.acceptanceStatus,
    matchingStatus: sp.matchingStatus,
  });

  const filterActive =
    Boolean(sp.receiptStatus) ||
    Boolean(sp.acceptanceStatus) ||
    Boolean(sp.matchingStatus);

  return (
    <AppShell terms={terms} active="documents">
      <section className="panel panel-wide">
        <h1>{docLabel}</h1>
        <p className="lede">
          Ba chiều độc lập: {receivedLabel} ≠ {acceptedLabel} ≠ {matchedLabel}.
          Không gộp thành một trạng thái; không đồng nghĩa Chi phí hay Thanh toán.
        </p>

        <div className="search-bar" role="group" aria-label="Bộ lọc chứng từ">
          <Link className="btn" href="/documents/receive">
            Nhận {docLabel.toLowerCase()}
          </Link>
          {filterActive ? (
            <Link className="btn btn-ghost" href="/documents">
              Xóa bộ lọc
            </Link>
          ) : null}
          <Link
            className="btn btn-ghost"
            href="/documents?acceptanceStatus=not_accepted&receiptStatus=received"
          >
            Chờ chấp nhận
          </Link>
          <Link
            className="btn btn-ghost"
            href="/documents?matchingStatus=unmatched&acceptanceStatus=accepted"
          >
            Đã chấp nhận — chưa khớp
          </Link>
        </div>

        {!result.ok ? (
          <div className="alert alert-error" role="alert">
            {result.message}
          </div>
        ) : result.data.length === 0 ? (
          <div className="empty-state" role="status">
            {filterActive
              ? "Không có chứng từ khớp bộ lọc. Thử xóa bộ lọc hoặc nhận chứng từ mới."
              : `Chưa có ${docLabel.toLowerCase()}. Nhận chứng từ để bắt đầu (chỉ đặt ${receivedLabel}).`}
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
                {result.data.map((doc) => (
                  <tr key={doc.id}>
                    <td>
                      <div className="queue-title">{doc.documentNo}</div>
                      <span className="muted small block">
                        {doc.documentDate}
                      </span>
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
      </section>
    </AppShell>
  );
}
