import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import {
  FilterBar,
  ListPageHeader,
  StatCardGrid,
  type StatCardModel,
} from "@/components/list";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology } from "@/lib/api";
import { formatDateVi } from "@/lib/money";
import { listSurcharges } from "@/lib/rate-cards-server";

type SearchParams = Promise<{ q?: string; mode?: string; status?: string }>;

export default async function SurchargesPage({
  searchParams,
}: {
  searchParams: SearchParams;
}) {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) redirect("/login");
  const terms = await fetchTerminology();
  const sp = await searchParams;
  const q = (sp.q ?? "").trim().toLowerCase();
  const mode = (sp.mode ?? "").trim().toLowerCase();
  const status = (sp.status ?? "").trim().toLowerCase();

  const rows = await listSurcharges();
  const all = rows.ok ? rows.data : [];
  const filtered = all.filter((row) => {
    if (q) {
      const hay = `${row.code} ${row.name}`.toLowerCase();
      if (!hay.includes(q)) return false;
    }
    if (mode && (row.transportMode ?? "").toLowerCase() !== mode) return false;
    if (status === "published" && row.versionStatus !== "published") return false;
    if (status === "draft" && row.versionStatus === "published") return false;
    return true;
  });

  const published = all.filter((r) => r.versionStatus === "published").length;
  const draft = all.length - published;
  const modes = new Set(all.map((r) => (r.transportMode || "").toLowerCase()).filter(Boolean));

  const kpis: StatCardModel[] = [
    { key: "all", label: "Tổng phụ phí", value: String(all.length), tone: "default" },
    { key: "pub", label: "Đang hiệu lực", value: String(published), tone: "success" },
    { key: "draft", label: "Trên phiên bản nháp", value: String(draft), tone: "warning" },
    { key: "modes", label: "Phương thức", value: String(modes.size || "—"), tone: "info" },
  ];

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
          lede="Phụ phí là thành phần quy tắc giá gắn vào phiên bản bảng giá. Published bất biến — sửa bằng phiên bản mới."
          action={
            <Link className="btn btn-primary" href="/rate-cards/new">
              ＋ Tạo trên bảng giá nháp
            </Link>
          }
        />

        <StatCardGrid cards={kpis} />

        <FilterBar
          action="/rate-cards/surcharges"
          resetHref={sp.q || sp.mode || sp.status ? "/rate-cards/surcharges" : undefined}
          fields={[
            {
              kind: "search",
              name: "q",
              label: "Tìm kiếm",
              placeholder: "Mã / tên phụ phí…",
              defaultValue: sp.q ?? "",
            },
            {
              kind: "select",
              name: "mode",
              label: "Phương thức",
              defaultValue: sp.mode ?? "",
              options: [
                { value: "", label: "Tất cả" },
                { value: "air", label: "Air" },
                { value: "sea", label: "Sea" },
                { value: "road", label: "Road" },
                { value: "rail", label: "Rail" },
              ],
            },
            {
              kind: "select",
              name: "status",
              label: "Trạng thái",
              defaultValue: sp.status ?? "",
              options: [
                { value: "", label: "Tất cả" },
                { value: "published", label: "Đang hiệu lực" },
                { value: "draft", label: "Nháp" },
              ],
            },
          ]}
        />

        <p className="muted">
          Không có form phụ phí độc lập — thêm thành phần trên phiên bản nháp của bảng giá, rồi phát hành.
        </p>

        {!rows.ok ? (
          <div className="alert alert-error" role="alert">
            {rows.message}
          </div>
        ) : filtered.length === 0 ? (
          <div className="empty-state" role="status">
            {all.length === 0
              ? "Chưa có phụ phí. Tạo bảng giá nháp và thêm thành phần quy tắc."
              : "Không khớp bộ lọc hiện tại."}
          </div>
        ) : (
          <div className="table-wrap">
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
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {filtered.map((row) => (
                  <tr key={row.id}>
                    <td>
                      <code>{row.code}</code>
                    </td>
                    <td>{row.name}</td>
                    <td>{row.calcMethod || "—"}</td>
                    <td>
                      {row.amount} {row.currencyCode}
                    </td>
                    <td>{row.transportMode || "—"}</td>
                    <td>
                      {formatDateVi(row.effectiveFrom)} – {formatDateVi(row.effectiveTo)}
                    </td>
                    <td>
                      {row.versionStatus === "published" ? "Đang hiệu lực" : "Nháp"} · {row.cardCode}{" "}
                      v{row.versionNo}
                    </td>
                    <td>
                      <Link className="row-link" href={`/rate-cards?q=${encodeURIComponent(row.cardCode)}`}>
                        Mở danh sách
                      </Link>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </AppShell>
  );
}
