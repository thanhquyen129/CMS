import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { CatalogHubNav } from "@/components/CatalogHubNav";
import { UpsertCommodityForm } from "@/components/UpsertCommodityForm";
import { ListPageHeader } from "@/components/list/ListPageHeader";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology } from "@/lib/api";
import { listCommodities } from "@/lib/reference-masters";

function flags(row: {
  isDangerousGoods: boolean;
  isTemperatureControlled: boolean;
  isOversize: boolean;
  isOverweight: boolean;
  isHighValue: boolean;
}): string {
  const parts = [
    row.isDangerousGoods ? "DG" : null,
    row.isTemperatureControlled ? "Lạnh" : null,
    row.isOversize ? "Quá khổ" : null,
    row.isOverweight ? "Quá tải" : null,
    row.isHighValue ? "Giá trị cao" : null,
  ].filter(Boolean);
  return parts.join(", ") || "—";
}

export default async function CommoditiesPage() {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) redirect("/login");
  const terms = await fetchTerminology();
  const result = await listCommodities(false);
  const rows = result.ok ? result.data : [];

  return (
    <AppShell terms={terms} active="admin">
      <section className="panel panel-wide">
        <ListPageHeader
          breadcrumbs={[{ href: "/admin", label: "Danh mục" }, { label: "Loại hàng" }]}
          title="Loại hàng"
          lede="Phân loại và cờ dùng cho điều kiện tính giá. Không theo dõi kho hay xếp dỡ."
        />
        <CatalogHubNav active="commodity" />
        <div className="layout-cols-2">
          <fieldset className="group-box">
            <legend>Danh sách</legend>
            {!result.ok ? <div className="alert alert-error">{result.message}</div> : null}
            {result.ok && rows.length === 0 ? <div className="empty-state">Chưa có loại hàng.</div> : null}
            {rows.length > 0 ? (
              <div className="table-wrap">
                <table className="data-table">
                  <thead>
                    <tr>
                      <th>Mã</th>
                      <th>Tên</th>
                      <th>Nhóm</th>
                      <th>Cờ</th>
                    </tr>
                  </thead>
                  <tbody>
                    {rows.map((row) => (
                      <tr key={row.id}>
                        <td>{row.code}</td>
                        <td>{row.name}</td>
                        <td>{row.category || "—"}</td>
                        <td>{flags(row)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            ) : null}
          </fieldset>
          <fieldset className="group-box">
            <legend>Thêm hoặc cập nhật</legend>
            <UpsertCommodityForm parents={rows} />
          </fieldset>
        </div>
      </section>
    </AppShell>
  );
}
