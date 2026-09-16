import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { DocumentStatusTriad } from "@/components/DocumentStatusTriad";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import { getBill } from "@/lib/bills";
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
  billId?: string;
}>;

function docsHref(opts: {
  billId?: string;
  receiptStatus?: string;
  acceptanceStatus?: string;
  matchingStatus?: string;
}): string {
  const p = new URLSearchParams();
  if (opts.billId) p.set("billId", opts.billId);
  if (opts.receiptStatus) p.set("receiptStatus", opts.receiptStatus);
  if (opts.acceptanceStatus) p.set("acceptanceStatus", opts.acceptanceStatus);
  if (opts.matchingStatus) p.set("matchingStatus", opts.matchingStatus);
  const qs = p.toString();
  return qs ? `/documents?${qs}` : "/documents";
}

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
  const billId = sp.billId?.trim() || undefined;
  const terms = await fetchTerminology();
  const docLabel = term(terms, "FINANCIAL_DOCUMENT", "Chứng từ tài chính");
  const billLabel = term(terms, "BILL", "Bill");
  const receivedLabel = term(terms, "RECEIVED", "Đã nhận");
  const acceptedLabel = term(terms, "ACCEPTED", "Đã chấp nhận");
  const matchedLabel = term(terms, "MATCHED", "Đã khớp");

  const [result, billRes] = await Promise.all([
    listFinancialDocuments({
      receiptStatus: sp.receiptStatus,
      acceptanceStatus: sp.acceptanceStatus,
      matchingStatus: sp.matchingStatus,
      billId,
    }),
    billId ? getBill(billId) : Promise.resolve(null),
  ]);

  const billNo =
    billRes && billRes.ok ? billRes.data.billNo : billId ? billId.slice(0, 8) + "…" : null;

  const filterActive =
    Boolean(billId) ||
    Boolean(sp.receiptStatus) ||
    Boolean(sp.acceptanceStatus) ||
    Boolean(sp.matchingStatus);

  return (
    <AppShell terms={terms} active="documents">
      <section className="panel panel-wide">
        <p className="breadcrumb">
          <Link href="/dashboard">Trang chủ</Link>
          {" / "}
          {docLabel}
        </p>
        <div className="page-header-row">
          <div>
            <h1>{docLabel}</h1>
            <p className="lede">
              Ba chiều độc lập: {receivedLabel} ≠ {acceptedLabel} ≠ {matchedLabel}.
              Không gộp thành một trạng thái; không đồng nghĩa Chi phí hay Thanh toán.
            </p>
          </div>
          <Link
            className="btn"
            href={
              billId
                ? `/documents/receive?billId=${encodeURIComponent(billId)}`
                : "/documents/receive"
            }
          >
            + Nhận chứng từ
          </Link>
        </div>

        {billId ? (
          <p className="note" role="status">
            Đang lọc theo {billLabel}
            {billNo ? (
              <>
                {" "}
                <Link className="row-link" href={`/bills/${billId}`}>
                  {billNo}
                </Link>
              </>
            ) : null}
            . Gồm chứng từ gắn header hoặc dòng có Bill này.
          </p>
        ) : null}

        <div className="search-bar" role="group" aria-label="Bộ lọc chứng từ">
          {filterActive ? (
            <Link className="btn btn-ghost" href="/documents">
              Xóa bộ lọc
            </Link>
          ) : null}
          <Link
            className="btn btn-ghost"
            href={docsHref({
              billId,
              acceptanceStatus: "not_accepted",
              receiptStatus: "received",
            })}
          >
            Chờ chấp nhận
          </Link>
          <Link
            className="btn btn-ghost"
            href={docsHref({
              billId,
              matchingStatus: "unmatched",
              acceptanceStatus: "accepted",
            })}
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
