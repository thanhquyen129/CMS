import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { DocumentListWorkspace } from "@/components/DocumentListWorkspace";
import { ListPagination } from "@/components/ListPagination";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import { getBill } from "@/lib/bills";
import { listFinancialDocuments } from "@/lib/documents";
import {
  parsePage,
  parsePageSize,
  totalPages as calcTotalPages,
} from "@/lib/list-paging";

type SearchParams = Promise<{
  receiptStatus?: string;
  acceptanceStatus?: string;
  matchingStatus?: string;
  documentType?: string;
  billId?: string;
  page?: string;
  pageSize?: string;
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
  const billId = sp.billId?.trim() || undefined;
  const documentType = sp.documentType?.trim() || undefined;
  const terms = await fetchTerminology();
  const docLabel = term(terms, "FINANCIAL_DOCUMENT", "Chứng từ tài chính");
  const billLabel = term(terms, "BILL", "Bill");
  const receivedLabel = term(terms, "RECEIVED", "Đã nhận");
  const acceptedLabel = term(terms, "ACCEPTED", "Đã chấp nhận");
  const matchedLabel = term(terms, "MATCHED", "Đã khớp");

  const pageSize = parsePageSize(sp.pageSize);
  const pageHint = Math.max(
    1,
    Number.parseInt(String(sp.page ?? "1"), 10) || 1
  );

  const [result, billRes] = await Promise.all([
    listFinancialDocuments({
      receiptStatus: sp.receiptStatus,
      acceptanceStatus: sp.acceptanceStatus,
      matchingStatus: sp.matchingStatus,
      documentType,
      billId,
      page: pageHint,
      pageSize,
    }),
    billId ? getBill(billId) : Promise.resolve(null),
  ]);

  const billNo =
    billRes && billRes.ok
      ? billRes.data.billNo
      : billId
        ? billId.slice(0, 8) + "…"
        : null;

  const filterActive =
    Boolean(billId) ||
    Boolean(documentType) ||
    Boolean(sp.receiptStatus) ||
    Boolean(sp.acceptanceStatus) ||
    Boolean(sp.matchingStatus);

  const totalCount = result.ok ? result.data.totalCount : 0;
  const pages = calcTotalPages(totalCount, pageSize);
  const page = parsePage(sp.page, pages);
  const pageRows = result.ok ? result.data.items : [];
  const pageParams = {
    billId,
    documentType,
    receiptStatus: sp.receiptStatus,
    acceptanceStatus: sp.acceptanceStatus,
    matchingStatus: sp.matchingStatus,
  };

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
              Không gộp thành một trạng thái. Chọn dòng để xem panel chi tiết.
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
            .
          </p>
        ) : null}

        <form className="search-bar denser-filters" method="get" action="/documents">
          {billId ? <input type="hidden" name="billId" value={billId} /> : null}
          <label className="sr-only" htmlFor="documentType">
            Loại chứng từ
          </label>
          <select
            id="documentType"
            name="documentType"
            defaultValue={documentType ?? ""}
          >
            <option value="">Tất cả loại</option>
            <option value="invoice">Hóa đơn</option>
            <option value="debit_note">Debit note</option>
            <option value="credit_note">Credit note</option>
            <option value="dn">DN</option>
            <option value="other">Khác</option>
          </select>
          <label className="sr-only" htmlFor="receiptStatus">
            {receivedLabel}
          </label>
          <select
            id="receiptStatus"
            name="receiptStatus"
            defaultValue={sp.receiptStatus ?? ""}
          >
            <option value="">Mọi trạng thái nhận</option>
            <option value="received">{receivedLabel}</option>
            <option value="not_received">Chưa nhận</option>
          </select>
          <label className="sr-only" htmlFor="acceptanceStatus">
            {acceptedLabel}
          </label>
          <select
            id="acceptanceStatus"
            name="acceptanceStatus"
            defaultValue={sp.acceptanceStatus ?? ""}
          >
            <option value="">Mọi trạng thái chấp nhận</option>
            <option value="accepted">{acceptedLabel}</option>
            <option value="not_accepted">Chưa chấp nhận</option>
          </select>
          <label className="sr-only" htmlFor="matchingStatus">
            {matchedLabel}
          </label>
          <select
            id="matchingStatus"
            name="matchingStatus"
            defaultValue={sp.matchingStatus ?? ""}
          >
            <option value="">Mọi trạng thái khớp</option>
            <option value="matched">{matchedLabel}</option>
            <option value="partially_matched">Khớp một phần</option>
            <option value="unmatched">Chưa khớp</option>
            <option value="draft">Nháp khớp</option>
          </select>
          <button className="btn" type="submit">
            Lọc
          </button>
          {filterActive ? (
            <Link className="btn btn-ghost" href="/documents">
              Làm mới
            </Link>
          ) : null}
        </form>

        <div className="filter-tabs" role="group" aria-label="Lọc nhanh">
          <Link
            className="btn btn-ghost btn-sm"
            href={`/documents?receiptStatus=received&acceptanceStatus=not_accepted${billId ? `&billId=${billId}` : ""}`}
          >
            Chờ chấp nhận
          </Link>
          <Link
            className="btn btn-ghost btn-sm"
            href={`/documents?acceptanceStatus=accepted&matchingStatus=unmatched${billId ? `&billId=${billId}` : ""}`}
          >
            Đã chấp nhận — chưa khớp
          </Link>
        </div>

        {!result.ok ? (
          <div className="alert alert-error" role="alert">
            {result.message}
          </div>
        ) : totalCount === 0 ? (
          <div className="empty-state" role="status">
            {filterActive
              ? "Không có chứng từ khớp bộ lọc."
              : `Chưa có ${docLabel.toLowerCase()}. Nhận chứng từ để bắt đầu.`}
          </div>
        ) : (
          <>
            <DocumentListWorkspace
              terms={terms}
              documents={pageRows}
              billLabel={billLabel}
              docLabel={docLabel}
            />
            <ListPagination
              basePath="/documents"
              params={pageParams}
              page={page}
              pageSize={pageSize}
              totalCount={totalCount}
              totalPages={pages}
            />
          </>
        )}
      </section>
    </AppShell>
  );
}
