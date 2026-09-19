import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { ListPageHeader } from "@/components/list/ListPageHeader";
import { UpsertCatalogItemForm } from "@/components/UpsertCatalogItemForm";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology } from "@/lib/api";
import {
  CATALOG_KINDS,
  catalogKindLabel,
  listCatalog,
} from "@/lib/catalog";

export default async function AdminCatalogPage() {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const terms = await fetchTerminology();
  const result = await listCatalog();

  return (
    <AppShell terms={terms} active="admin">
      <section className="panel panel-wide">
        <ListPageHeader
          breadcrumbs={[
            { href: "/dashboard", label: "Trang chủ" },
            { href: "/admin", label: "Danh mục" },
            { label: "Loại nghiệp vụ" },
          ]}
          title="Loại chi phí / doanh thu / dịch vụ"
          lede="Danh mục tenant dùng trên chi phí, doanh thu, bảng giá và chứng từ. Không xóa — ngưng dùng bằng cập nhật."
        />

        <div className="layout-cols-2">
          <fieldset className="group-box">
            <legend>Danh sách</legend>
            {!result.ok ? (
              <div className="alert alert-error" role="alert">
                {result.message}
              </div>
            ) : result.data.length === 0 ? (
              <div className="empty-state" role="status">
                Chưa có mã danh mục.
              </div>
            ) : (
              <div className="table-wrap">
                <table className="data-table">
                  <thead>
                    <tr>
                      <th scope="col">Nhóm</th>
                      <th scope="col">Mã</th>
                      <th scope="col">Tên</th>
                      <th scope="col">Trạng thái</th>
                    </tr>
                  </thead>
                  <tbody>
                    {result.data.map((i) => (
                      <tr key={i.id}>
                        <td>{catalogKindLabel(i.kind)}</td>
                        <td className="mono-id">{i.code}</td>
                        <td>
                          {i.name}
                          {i.description ? (
                            <div className="muted small">{i.description}</div>
                          ) : null}
                        </td>
                        <td>{i.isActive ? "Đang dùng" : "Ngừng"}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
            <p className="muted small">
              Nhóm: {CATALOG_KINDS.map((k) => k.label).join(" · ")}
            </p>
          </fieldset>
          <fieldset className="group-box">
            <legend>Thêm / cập nhật</legend>
            <UpsertCatalogItemForm />
          </fieldset>
        </div>
      </section>
    </AppShell>
  );
}
