import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { CatalogHubNav } from "@/components/CatalogHubNav";
import { CreateCurrencyForm } from "@/components/CreateCurrencyForm";
import { SyncVcbFxButton } from "@/components/SyncVcbFxButton";
import { ListPageHeader } from "@/components/list/ListPageHeader";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import { listCurrencies } from "@/lib/master-data";

const POPULAR = new Set([
  "VND",
  "USD",
  "EUR",
  "CNY",
  "JPY",
  "KRW",
  "SGD",
  "THB",
  "GBP",
  "AUD",
  "HKD",
  "TWD",
  "CHF",
  "CAD",
  "MYR",
]);

export default async function AdminCurrenciesPage() {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const terms = await fetchTerminology();
  const dashboardLabel = term(terms, "DASHBOARD", "Bảng điều khiển");
  const result = await listCurrencies();
  const all = result.ok ? result.data : [];
  const popular = all.filter((c) => POPULAR.has(c.code.toUpperCase()));
  const others = all.filter((c) => !POPULAR.has(c.code.toUpperCase()));

  return (
    <AppShell terms={terms} active="admin">
      <section className="panel panel-wide">
        <ListPageHeader
          breadcrumbs={[
            { href: "/dashboard", label: dashboardLabel },
            { href: "/admin", label: "Danh mục" },
            { label: "Tiền tệ & Tỷ giá" },
          ]}
          title="Tiền tệ & Tỷ giá"
          lede="Danh mục tiền tệ phổ biến cho logistics Việt Nam. Tỷ giá ngày có thể lấy từ Vietcombank hoặc nhập tay."
        />
        <CatalogHubNav active="currency" />

        <div className="layout-cols-2" style={{ marginBottom: "1.25rem" }}>
          <fieldset className="group-box">
            <legend>Tỷ giá Vietcombank</legend>
            <p className="note">
              Lấy tỷ giá chuyển khoản ngoại tệ → VND từ cổng công khai VCB, ghi vào sổ tỷ giá theo ngày.
              Không tính lại chứng từ đã ghi. Chi tiết lịch sử:{" "}
              <Link className="row-link" href="/rate-cards/fx">
                Quản lý tỷ giá
              </Link>
              .
            </p>
            <SyncVcbFxButton />
          </fieldset>
          <fieldset className="group-box">
            <legend>Thêm / cập nhật tiền tệ</legend>
            <CreateCurrencyForm />
          </fieldset>
        </div>

        {!result.ok ? (
          <div className="alert alert-error" role="alert">
            {result.message}
          </div>
        ) : all.length === 0 ? (
          <div className="empty-state" role="status">
            Chưa có tiền tệ. Khởi động lại API để seed danh mục phổ biến, hoặc thêm thủ công.
          </div>
        ) : (
          <>
            <h2 className="section-title sm">Tiền tệ phổ biến</h2>
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
                  {popular.map((c) => (
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

            {others.length > 0 ? (
              <>
                <h2 className="section-title sm">Khác</h2>
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
                      {others.map((c) => (
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
              </>
            ) : null}
          </>
        )}
      </section>
    </AppShell>
  );
}
