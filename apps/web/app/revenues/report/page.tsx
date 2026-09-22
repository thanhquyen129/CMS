import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology } from "@/lib/api";
import { listProfitGroups } from "@/lib/costs-revenues-server";
import { formatMoney } from "@/lib/money";

type SearchParams = Promise<{ groupBy?: string; view?: string }>;

const groups = [
  ["customer", "Khách hàng"],
  ["service", "Dịch vụ"],
  ["mode", "Phương thức"],
  ["route", "Tuyến"],
  ["movement", "Chuyến"],
] as const;

const views = [
  ["expected", "Dự kiến"],
  ["confirmed", "Đã xác nhận"],
  ["actual", "Thực tế"],
] as const;

export default async function RevenueReportPage({
  searchParams,
}: {
  searchParams: SearchParams;
}) {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const query = await searchParams;
  const groupBy = groups.some((g) => g[0] === query.groupBy) ? query.groupBy! : "customer";
  const view = views.some((v) => v[0] === query.view) ? query.view! : "actual";
  const terms = await fetchTerminology();
  const rows = await listProfitGroups(groupBy, view);

  return (
    <AppShell terms={terms} active="revenues">
      <section className="panel panel-wide">
        <p className="meta-line">
          <Link href="/revenues">Danh sách doanh thu</Link>
          <span aria-hidden="true"> / </span>
          <span>Báo cáo doanh thu</span>
        </p>
        <h1>Báo cáo doanh thu</h1>
        <p className="lede">
          Tổng hợp theo một lớp maturity từ API lợi nhuận. Bill nhiều tiền tệ không được cộng vào nhóm.
        </p>
        <p className="cta-row">
          {groups.map(([id, label]) => (
            <Link key={id} className={id === groupBy ? "btn btn-sm" : "btn btn-sm btn-ghost"} href={`/revenues/report?groupBy=${id}&view=${view}`}>
              {label}
            </Link>
          ))}
        </p>
        <p className="cta-row">
          {views.map(([id, label]) => (
            <Link key={id} className={id === view ? "btn btn-sm" : "btn btn-sm btn-ghost"} href={`/revenues/report?groupBy=${groupBy}&view=${id}`}>
              {label}
            </Link>
          ))}
        </p>
        {!rows.ok ? (
          <div className="alert alert-error" role="alert">{rows.message}</div>
        ) : rows.data.length === 0 ? (
          <div className="empty-state" role="status">Chưa có số để tổng hợp.</div>
        ) : (
          <div className="table-wrap">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Nhóm</th>
                  <th className="num">Doanh thu</th>
                  <th className="num">Chi phí</th>
                  <th className="num">Lợi nhuận</th>
                  <th className="num">Tỷ suất</th>
                  <th className="num">Số bill</th>
                </tr>
              </thead>
              <tbody>
                {rows.data.map((row) => (
                  <tr key={row.key}>
                    <td>
                      {row.label}
                      {row.note ? <div className="note">{row.note}</div> : null}
                    </td>
                    <td className="num">{formatMoney(row.revenueAmount, row.currencyCode)}</td>
                    <td className="num">{formatMoney(row.costAmount, row.currencyCode)}</td>
                    <td className="num">{formatMoney(row.profitAmount, row.currencyCode)}</td>
                    <td className="num">{row.marginRate == null ? "N/A" : `${row.marginRate.toFixed(2)}%`}</td>
                    <td className="num">{row.billCount}</td>
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
