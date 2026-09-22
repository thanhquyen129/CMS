import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { ListPageHeader } from "@/components/list";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology } from "@/lib/api";
import { formatDateVi } from "@/lib/money";
import { listSurcharges } from "@/lib/rate-cards-server";

export default async function SurchargesPage() {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) redirect("/login");
  const terms = await fetchTerminology();
  const rows = await listSurcharges();

  return (
    <AppShell terms={terms} active="rate-cards">
      <section className="panel panel-wide">
        <ListPageHeader
          breadcrumbs={[
            { href: "/dashboard", label: "Trang chủ" },
            { href: "/rate-cards", label: "Bảng giá & Tính giá" },
            { label: "Quản lý phụ phí" },
          ]}
          title="Quản lý phụ phí"
          lede="Quản lý surcharge/component gắn vào Rate Version / Pricing Rule"
          action={<Link className="btn" href="/rate-cards/new">＋ Tạo phụ phí</Link>}
        />
        <p className="muted">
          Phụ phí là thành phần của quy tắc giá. Phiên bản đã Published không sửa trực tiếp — thay đổi có hiệu lực phải tạo phiên bản mới.
        </p>
        {!rows.ok ? (
          <div className="alert alert-error" role="alert">{rows.message}</div>
        ) : rows.data.length === 0 ? (
          <div className="empty-state">Chưa có phụ phí. Thêm thành phần trên một phiên bản bảng giá đang nháp.</div>
        ) : (
          <table className="data-table">
            <thead>
              <tr>
                <th>Mã</th>
                <th>Tên phụ phí</th>
                <th>Cách tính</th>
                <th>Đơn vị</th>
                <th>Phương thức</th>
                <th>Hiệu lực</th>
                <th>Trạng thái</th>
              </tr>
            </thead>
            <tbody>
              {rows.data.map((row) => (
                <tr key={row.id}>
                  <td><code>{row.code}</code></td>
                  <td>{row.name}</td>
                  <td>{row.calcMethod || "—"}</td>
                  <td>{row.amount} {row.currencyCode}</td>
                  <td>{row.transportMode || "—"}</td>
                  <td>{formatDateVi(row.effectiveFrom)} – {formatDateVi(row.effectiveTo)}</td>
                  <td>{row.versionStatus === "published" ? "Đang hiệu lực" : "Nháp"} · {row.cardCode} v{row.versionNo}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>
    </AppShell>
  );
}
