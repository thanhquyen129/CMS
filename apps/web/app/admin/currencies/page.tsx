import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { CreateCurrencyForm } from "@/components/CreateCurrencyForm";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import { listCurrencies } from "@/lib/master-data";

export default async function AdminCurrenciesPage() {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const terms = await fetchTerminology();
  const dashboardLabel = term(terms, "DASHBOARD", "Bảng điều khiển");
  const result = await listCurrencies();

  return (
    <AppShell terms={terms} active="admin">
      <section className="panel panel-wide">
        <p className="breadcrumb">
          <Link href="/dashboard">{dashboardLabel}</Link>
          {" / "}
          <Link href="/admin">Danh mục</Link>
          {" / "}
          Tiền tệ
        </p>
        <h1>Tiền tệ</h1>
        <p className="lede">
          Danh mục tiền tệ dùng trên chi phí, doanh thu, thanh toán.
        </p>

        <div className="layout-cols-2">
          <fieldset className="group-box">
            <legend>Danh sách</legend>
            {!result.ok ? (
              <div className="alert alert-error" role="alert">
                {result.message}
              </div>
            ) : result.data.length === 0 ? (
              <div className="empty-state" role="status">
                Chưa có tiền tệ.
              </div>
            ) : (
              <div className="table-wrap">
                <table className="data-table">
                  <thead>
                    <tr>
                      <th scope="col">Mã</th>
                      <th scope="col">Tên</th>
                      <th scope="col">Số lẻ</th>
                      <th scope="col">Trạng thái</th>
                    </tr>
                  </thead>
                  <tbody>
                    {result.data.map((c) => (
                      <tr key={c.id}>
                        <td className="mono-id">{c.code}</td>
                        <td>{c.name}</td>
                        <td>{c.decimalPlaces}</td>
                        <td>{c.isActive ? "Đang dùng" : "Ngừng"}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </fieldset>

          <fieldset className="group-box">
            <legend>Thêm / cập nhật</legend>
            <CreateCurrencyForm />
          </fieldset>
        </div>
      </section>
    </AppShell>
  );
}
