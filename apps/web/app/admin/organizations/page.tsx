import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { CreateOrganizationForm } from "@/components/CreateOrganizationForm";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import { listOrganizations } from "@/lib/master-data";

export default async function AdminOrganizationsPage() {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const terms = await fetchTerminology();
  const dashboardLabel = term(terms, "DASHBOARD", "Bảng điều khiển");
  const result = await listOrganizations();

  return (
    <AppShell terms={terms} active="admin">
      <section className="panel panel-wide">
        <p className="breadcrumb">
          <Link href="/dashboard">{dashboardLabel}</Link>
          {" / "}
          <Link href="/admin">Danh mục</Link>
          {" / "}
          Đơn vị
        </p>
        <h1>Đơn vị / Tổ chức</h1>
        <p className="lede">
          Cây tổ chức phục vụ phạm vi dữ liệu. Thêm đơn vị gốc (không cha) tại
          đây; gán cha qua API sau nếu cần.
        </p>

        {!result.ok ? (
          <div className="alert alert-error" role="alert">
            {result.message}
          </div>
        ) : result.data.length === 0 ? (
          <div className="empty-state" role="status">
            Chưa có đơn vị. Thêm bên dưới.
          </div>
        ) : (
          <div className="table-wrap">
            <table className="data-table">
              <thead>
                <tr>
                  <th scope="col">Mã</th>
                  <th scope="col">Tên</th>
                  <th scope="col">Trạng thái</th>
                </tr>
              </thead>
              <tbody>
                {result.data.map((o) => (
                  <tr key={o.id}>
                    <td className="mono-id">{o.code}</td>
                    <td>{o.name}</td>
                    <td>{o.isActive ? "Đang dùng" : "Ngừng"}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        <h2 className="section-title">Thêm đơn vị</h2>
        <CreateOrganizationForm />
      </section>
    </AppShell>
  );
}
