import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { ListPageHeader } from "@/components/list";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology } from "@/lib/api";
import { formatDateTimeVi, formatMoney } from "@/lib/money";
import { listRatingHistory } from "@/lib/rate-cards-server";

export default async function RateHistoryPage() {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) redirect("/login");
  const terms = await fetchTerminology();
  const rows = await listRatingHistory();

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
        </p>
        {!rows.ok ? (
          <div className="alert alert-error" role="alert">{rows.message}</div>
        ) : rows.data.length === 0 ? (
          <div className="empty-state">Chưa có lần tính giá.</div>
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
              {rows.data.map((row) => (
                <tr key={row.id}>
                  <td>{formatDateTimeVi(row.ratedAt)}</td>
                  <td>{row.cardCode || "—"}</td>
                  <td>{row.versionNo == null ? "—" : `v${row.versionNo}`}</td>
                  <td>{row.status}</td>
                  <td>{formatMoney(row.totalAmount, row.currencyCode)}</td>
                  <td><Link href={`/bills/${row.billId}`}>{row.billId.slice(0, 8)}</Link></td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>
    </AppShell>
  );
}
