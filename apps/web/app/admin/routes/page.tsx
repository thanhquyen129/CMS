import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { CatalogHubNav } from "@/components/CatalogHubNav";
import { UpsertRouteForm } from "@/components/UpsertRouteForm";
import { ListPageHeader } from "@/components/list/ListPageHeader";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology } from "@/lib/api";
import { listLocations, listRoutes } from "@/lib/reference-masters";

export default async function RoutesPage() {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) redirect("/login");
  const terms = await fetchTerminology();
  const [locations, routes] = await Promise.all([listLocations(false), listRoutes(false)]);
  const rows = routes.ok ? routes.data : [];

  return (
    <AppShell terms={terms} active="admin">
      <section className="panel panel-wide">
        <ListPageHeader
          breadcrumbs={[{ href: "/admin", label: "Danh mục" }, { label: "Tuyến" }]}
          title="Tuyến vận chuyển"
          lede="Tuyến là cặp địa điểm dùng lại cho bảng giá. Bill vẫn có thể lưu điểm đi và điểm đến mà không chọn tuyến."
        />
        <CatalogHubNav active="transport_route" />
        <div className="layout-cols-2">
          <fieldset className="group-box">
            <legend>Danh sách</legend>
            {!routes.ok ? <div className="alert alert-error">{routes.message}</div> : null}
            {routes.ok && rows.length === 0 ? <div className="empty-state">Chưa có tuyến.</div> : null}
            {rows.length > 0 ? (
              <div className="table-wrap">
                <table className="data-table">
                  <thead>
                    <tr>
                      <th>Mã</th>
                      <th>Tên</th>
                      <th>Điểm đi</th>
                      <th>Trung gian</th>
                      <th>Điểm đến</th>
                    </tr>
                  </thead>
                  <tbody>
                    {rows.map((row) => (
                      <tr key={row.id}>
                        <td>{row.code}</td>
                        <td>{row.name}</td>
                        <td>{row.originCode}</td>
                        <td>{row.stops.map((s) => s.locationCode).join(" → ") || "—"}</td>
                        <td>{row.destinationCode}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            ) : null}
          </fieldset>
          <fieldset className="group-box">
            <legend>Thêm hoặc cập nhật</legend>
            <UpsertRouteForm locations={locations.ok ? locations.data : []} />
          </fieldset>
        </div>
      </section>
    </AppShell>
  );
}
