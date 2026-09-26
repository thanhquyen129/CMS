import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { CatalogHubNav } from "@/components/CatalogHubNav";
import { ListPagination } from "@/components/ListPagination";
import { UpsertLocationForm } from "@/components/UpsertLocationForm";
import { ListPageHeader } from "@/components/list/ListPageHeader";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology } from "@/lib/api";
import {
  parsePage,
  parsePageSize,
  slicePage,
  totalPages as calcTotalPages,
} from "@/lib/list-paging";
import { listLocations, locationTypeLabel } from "@/lib/reference-masters";

type SearchParams = Promise<{ page?: string; pageSize?: string }>;

export default async function LocationsPage({
  searchParams,
}: {
  searchParams: SearchParams;
}) {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) redirect("/login");
  const terms = await fetchTerminology();
  const sp = await searchParams;
  const pageSize = parsePageSize(sp.pageSize);
  const result = await listLocations(false);
  const rows = result.ok ? result.data : [];
  const pages = calcTotalPages(rows.length, pageSize);
  const page = parsePage(sp.page, pages);
  const pageRows = slicePage(rows, page, pageSize);

  return (
    <AppShell terms={terms} active="admin">
      <section className="panel panel-wide">
        <ListPageHeader
          breadcrumbs={[{ href: "/admin", label: "Danh mục" }, { label: "Địa điểm" }]}
          title="Địa điểm"
          lede="Mã canonical cho điểm đi và điểm đến. Alias, IATA và UN/LOCODE cùng trỏ về một địa điểm."
        />
        <CatalogHubNav active="location" />
        <div className="layout-cols-2">
          <fieldset className="group-box">
            <legend>Danh sách</legend>
            {!result.ok ? <div className="alert alert-error">{result.message}</div> : null}
            {result.ok && rows.length === 0 ? <div className="empty-state">Chưa có địa điểm.</div> : null}
            {pageRows.length > 0 ? (
              <>
                <div className="table-wrap">
                  <table className="data-table">
                    <thead>
                      <tr>
                        <th>Mã</th>
                        <th>Tên</th>
                        <th>Loại</th>
                        <th>IATA / UNLOCODE</th>
                        <th>Alias</th>
                        <th>Trạng thái</th>
                      </tr>
                    </thead>
                    <tbody>
                      {pageRows.map((row) => (
                        <tr key={row.id}>
                          <td>{row.code}</td>
                          <td>{row.name}</td>
                          <td>{locationTypeLabel(row.locationType)}</td>
                          <td>{[row.iataCode, row.unlocode].filter(Boolean).join(" / ") || "—"}</td>
                          <td>{row.aliases.map((a) => a.aliasCode).join(", ") || "—"}</td>
                          <td>{row.isActive ? "Đang dùng" : "Ngừng"}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
                <ListPagination
                  basePath="/admin/locations"
                  params={{}}
                  page={page}
                  pageSize={pageSize}
                  totalCount={rows.length}
                  totalPages={pages}
                />
              </>
            ) : null}
          </fieldset>
          <fieldset className="group-box">
            <legend>Thêm hoặc cập nhật</legend>
            <UpsertLocationForm />
          </fieldset>
        </div>
      </section>
    </AppShell>
  );
}
