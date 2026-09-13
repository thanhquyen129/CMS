import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { CreateBusinessPartyForm } from "@/components/CreateBusinessPartyForm";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import { listAdminParties } from "@/lib/master-data";
import { partyLabel } from "@/lib/party";

export default async function AdminPartiesPage() {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const terms = await fetchTerminology();
  const dashboardLabel = term(terms, "DASHBOARD", "Bảng điều khiển");
  const result = await listAdminParties();

  return (
    <AppShell terms={terms} active="admin">
      <section className="panel panel-wide">
        <p className="breadcrumb">
          <Link href="/dashboard">{dashboardLabel}</Link>
          {" / "}
          <Link href="/admin">Danh mục</Link>
          {" / "}
          Đối tác
        </p>
        <h1>Đối tác kinh doanh</h1>
        <p className="lede">
          Danh sách đối tác trong thuê bao. Thêm mới bằng mã và tên — không có
          loại đối tác riêng ở API hiện tại.
        </p>

        {!result.ok ? (
          <div className="alert alert-error" role="alert">
            {result.message}
          </div>
        ) : result.data.length === 0 ? (
          <div className="empty-state" role="status">
            Chưa có đối tác. Thêm bên dưới.
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
                {result.data.map((p) => (
                  <tr key={p.id}>
                    <td className="mono-id">{p.code}</td>
                    <td>{partyLabel(p)}</td>
                    <td>{p.isActive ? "Đang dùng" : "Ngừng"}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        <h2 className="section-title">Thêm đối tác</h2>
        <CreateBusinessPartyForm />
      </section>
    </AppShell>
  );
}
