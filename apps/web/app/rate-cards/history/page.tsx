import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { ExportCsvButton, FilterBar, ListPageHeader } from "@/components/list";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology } from "@/lib/api";
import { formatDateTimeVi, formatMoney } from "@/lib/money";
import { listRatingHistory, type RatingHistoryRow } from "@/lib/rate-cards-server";

type SearchParams = Promise<{ from?: string; to?: string }>;

function datePart(iso: string): string {
  return iso.slice(0, 10);
}

function filterByDateRange(
  rows: RatingHistoryRow[],
  from?: string,
  to?: string
): RatingHistoryRow[] {
  if (!from && !to) return rows;
  return rows.filter((row) => {
    const d = datePart(row.ratedAt);
    if (from && d < from) return false;
    if (to && d > to) return false;
    return true;
  });
}

export default async function RateHistoryPage({
  searchParams,
}: {
  searchParams: SearchParams;
}) {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) redirect("/login");
  const sp = await searchParams;
  const from = sp.from?.trim() || undefined;
  const to = sp.to?.trim() || undefined;
  const terms = await fetchTerminology();
  const rows = await listRatingHistory();
  const filtered = rows.ok ? filterByDateRange(rows.data, from, to) : [];
  const csvRows = filtered.map((row) => [
    formatDateTimeVi(row.ratedAt),
    row.cardCode || "",
    row.versionNo == null ? "" : `v${row.versionNo}`,
    row.status,
    row.totalAmount,
    row.currencyCode,
    row.billId,
  ]);

  return (
    <AppShell terms={terms} active="rate-cards">
      <section className="panel panel-wide">
        <ListPageHeader
          breadcrumbs={[
            { href: "/dashboard", label: "Trang chủ" },
            { href: "/rate-cards", label: "Bảng giá & Tính giá" },
            { label: "Lịch sử giá" },
          ]}
          title="Lịch sử giá"
          lede="Tra cứu thay đổi Rate Card, phiên bản, trạng thái và các lần Rating theo thời gian"
        />
        <p className="muted">
          Rating đã tạo tiếp tục tham chiếu đúng Rate Version và Rating Context Snapshot tại thời điểm tính.
          Lọc theo ngày trên danh sách đã tải (API chưa nhận from/to).
        </p>
        {!rows.ok ? (
          <div className="alert alert-error" role="alert">{rows.message}</div>
        ) : (
          <>
            <FilterBar
              action="/rate-cards/history"
              showLabels
              resetHref="/rate-cards/history"
              fields={[
                {
                  kind: "date",
                  name: "from",
                  label: "Từ ngày",
                  defaultValue: from ?? "",
                },
                {
                  kind: "date",
                  name: "to",
                  label: "Đến ngày",
                  defaultValue: to ?? "",
                },
              ]}
              extra={
                <ExportCsvButton
                  label="Xuất CSV"
                  filename={`rating-history-${new Date().toISOString().slice(0, 10)}.csv`}
                  headers={[
                    "Thời điểm",
                    "Rate Card",
                    "Phiên bản",
                    "Trạng thái",
                    "Thành tiền",
                    "Tiền tệ",
                    "Bill Id",
                  ]}
                  rows={csvRows}
                />
              }
            />
            {filtered.length === 0 ? (
              <div className="empty-state">
                {rows.data.length === 0
                  ? "Chưa có lần tính giá."
                  : "Không có lần tính giá trong khoảng ngày đã chọn."}
              </div>
            ) : (
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Thời điểm</th>
                    <th>Rate Card</th>
                    <th>Phiên bản</th>
                    <th>Trạng thái</th>
                    <th>Thành tiền</th>
                    <th>Bill</th>
                  </tr>
                </thead>
                <tbody>
                  {filtered.map((row) => (
                    <tr key={row.id}>
                      <td>{formatDateTimeVi(row.ratedAt)}</td>
                      <td>{row.cardCode || "—"}</td>
                      <td>{row.versionNo == null ? "—" : `v${row.versionNo}`}</td>
                      <td>{row.status}</td>
                      <td>{formatMoney(row.totalAmount, row.currencyCode)}</td>
                      <td>
                        <Link href={`/bills/${row.billId}`}>{row.billId.slice(0, 8)}</Link>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </>
        )}
      </section>
    </AppShell>
  );
}
