import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { UpsertFxRateForm } from "@/components/UpsertFxRateForm";
import { ListPageHeader } from "@/components/list";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology } from "@/lib/api";
import { listFxRates } from "@/lib/catalog";
import { formatDateVi } from "@/lib/money";

export default async function RateFxPage() {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) redirect("/login");
  const terms = await fetchTerminology();
  const result = await listFxRates();
  const rows = result.ok ? result.data : [];
  const latest = new Map<string, (typeof rows)[number]>();
  for (const row of rows) {
    const key = `${row.fromCurrencyCode}/${row.toCurrencyCode}`;
    const prev = latest.get(key);
    if (!prev || row.rateDate > prev.rateDate) latest.set(key, row);
  }

  return (
    <AppShell terms={terms} active="rate-cards">
      <section className="panel panel-wide">
        <ListPageHeader
          breadcrumbs={[
            { href: "/dashboard", label: "Trang chủ" },
            { href: "/rate-cards", label: "Bảng giá & Tính giá" },
            { label: "Quản lý tỷ giá" },
          ]}
          title="Quản lý tỷ giá"
          lede="Quản lý tỷ giá phục vụ Rating và quy đổi báo cáo theo chính sách Tenant"
        />
        <div className="stat-grid">
          {[...latest.values()].slice(0, 4).map((row) => (
            <article key={row.id} className="stat-card">
              <p>{row.fromCurrencyCode} / {row.toCurrencyCode}</p>
              <strong>{row.rate}</strong>
              <p className="muted">{formatDateVi(row.rateDate)} · {row.source}</p>
            </article>
          ))}
        </div>
        <div className="layout-cols-2">
          <div>
            {!result.ok ? (
              <div className="alert alert-error" role="alert">{result.message}</div>
            ) : rows.length === 0 ? (
              <div className="empty-state">Chưa có tỷ giá.</div>
            ) : (
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Từ</th>
                    <th>Sang</th>
                    <th>Tỷ giá</th>
                    <th>Ngày hiệu lực</th>
                    <th>Nguồn</th>
                    <th>Trạng thái</th>
                  </tr>
                </thead>
                <tbody>
                  {rows.map((row) => (
                    <tr key={row.id}>
                      <td>{row.fromCurrencyCode}</td>
                      <td>{row.toCurrencyCode}</td>
                      <td>{row.rate}</td>
                      <td>{formatDateVi(row.rateDate)}</td>
                      <td>{row.source}</td>
                      <td>Hiệu lực</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
            <p className="muted">
              Rating và báo cáo lưu tỷ giá đã dùng để tái hiện kết quả lịch sử. Không tính lại chứng từ đã ghi.
            </p>
          </div>
          <UpsertFxRateForm />
        </div>
      </section>
    </AppShell>
  );
}
