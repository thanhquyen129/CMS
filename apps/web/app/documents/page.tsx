import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { DocumentListWorkspace } from "@/components/DocumentListWorkspace";
import { ListPagination } from "@/components/ListPagination";
import {
  FilterBar,
  ListPageHeader,
  StatCardGrid,
} from "@/components/list";
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

  const [result, kpiRes, billRes] = await Promise.all([
    listFinancialDocuments({
      receiptStatus: sp.receiptStatus,
      acceptanceStatus: sp.acceptanceStatus,
      matchingStatus: sp.matchingStatus,
      documentType,
      billId,
      page: pageHint,
      pageSize,
    }),
    listFinancialDocuments({ billId, documentType }),
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

  const kpiItems = kpiRes.ok ? kpiRes.data.items : [];
  const receivedCount = kpiItems.filter(
    (d) => d.receiptStatus?.toLowerCase() === "received"
  ).length;
  const acceptedCount = kpiItems.filter(
    (d) => d.acceptanceStatus?.toLowerCase() === "accepted"
  ).length;
  const matchedCount = kpiItems.filter(
    (d) => d.matchingStatus?.toLowerCase() === "matched"
  ).length;
  const awaitAcceptCount = kpiItems.filter(
    (d) =>
      d.receiptStatus?.toLowerCase() === "received" &&
      d.acceptanceStatus?.toLowerCase() === "not_accepted"
  ).length;

  const resetHref = filterActive
    ? billId
      ? `/documents?billId=${encodeURIComponent(billId)}`
      : "/documents"
    : undefined;

  return (
    <AppShell terms={terms} active="documents">
      <section className="panel panel-wide">
        <ListPageHeader
          breadcrumbs={[
            { href: "/dashboard", label: "Trang chủ" },
            { label: docLabel },
          ]}
          title={docLabel}
          lede={
            <>
              Ba chiều độc lập: {receivedLabel} ≠ {acceptedLabel} ≠ {matchedLabel}.
              Không gộp thành một trạng thái. Chọn dòng để xem panel chi tiết.
            </>
          }
          action={
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
          }
        />

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

        {kpiRes.ok ? (
          <StatCardGrid
            cards={[
              {
                key: "total",
                label: `Tổng ${docLabel.toLowerCase()}`,
                value: kpiRes.data.totalCount,
                hint: billId ? `Theo ${billLabel}` : "Trong phạm vi của bạn",
              },
              {
                key: "received",
                label: receivedLabel,
                value: receivedCount,
                tone: "primary",
                href: `/documents?receiptStatus=received${billId ? `&billId=${billId}` : ""}`,
              },
              {
                key: "await",
                label: "Chờ chấp nhận",
                value: awaitAcceptCount,
                tone: awaitAcceptCount > 0 ? "warning" : "default",
                href: `/documents?receiptStatus=received&acceptanceStatus=not_accepted${billId ? `&billId=${billId}` : ""}`,
              },
              {
                key: "accepted",
                label: acceptedLabel,
                value: acceptedCount,
                tone: "success",
                href: `/documents?acceptanceStatus=accepted${billId ? `&billId=${billId}` : ""}`,
              },
              {
                key: "matched",
                label: matchedLabel,
                value: matchedCount,
                tone: "info",
                href: `/documents?matchingStatus=matched${billId ? `&billId=${billId}` : ""}`,
              },
            ]}
          />
        ) : null}

        <FilterBar
          action="/documents"
          resetHref={resetHref}
          hidden={{ billId }}
          fields={[
            {
              kind: "select",
              name: "documentType",
              label: "Loại chứng từ",
              defaultValue: documentType,
              emptyLabel: "Tất cả loại",
              options: [
                { value: "invoice", label: "Hóa đơn" },
                { value: "debit_note", label: "Debit note" },
                { value: "credit_note", label: "Credit note" },
                { value: "dn", label: "DN" },
                { value: "other", label: "Khác" },
              ],
            },
            {
              kind: "select",
              name: "receiptStatus",
              label: receivedLabel,
              defaultValue: sp.receiptStatus,
              emptyLabel: "Mọi trạng thái nhận",
              options: [
                { value: "received", label: receivedLabel },
                { value: "not_received", label: "Chưa nhận" },
              ],
            },
            {
              kind: "select",
              name: "acceptanceStatus",
              label: acceptedLabel,
              defaultValue: sp.acceptanceStatus,
              emptyLabel: "Mọi trạng thái chấp nhận",
              options: [
                { value: "accepted", label: acceptedLabel },
                { value: "not_accepted", label: "Chưa chấp nhận" },
              ],
            },
            {
              kind: "select",
              name: "matchingStatus",
              label: matchedLabel,
              defaultValue: sp.matchingStatus,
              emptyLabel: "Mọi trạng thái khớp",
              options: [
                { value: "matched", label: matchedLabel },
                { value: "partially_matched", label: "Khớp một phần" },
                { value: "unmatched", label: "Chưa khớp" },
                { value: "draft", label: "Nháp khớp" },
              ],
            },
          ]}
          extra={
            <span className="filter-tabs" role="group" aria-label="Lọc nhanh">
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
            </span>
          }
        />

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
