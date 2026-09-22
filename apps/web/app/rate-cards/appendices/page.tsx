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
import { listAppendices } from "@/lib/rate-cards-server";

type SearchParams = Promise<{ q?: string; status?: string }>;

function statusLabel(status: string): string {
  if (status === "published") return "Đã Published";
  if (status === "draft") return "Nháp";
  return status;
}

export default async function AppendicesPage({
  searchParams,
}: {
  searchParams: SearchParams;
}) {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) redirect("/login");
  const terms = await fetchTerminology();
  const sp = await searchParams;
  const q = (sp.q ?? "").trim().toLowerCase();
  const status = (sp.status ?? "").trim().toLowerCase();

  const rows = await listAppendices();
  const all = rows.ok ? rows.data : [];
  const filtered = all.filter((row) => {
    if (q) {
      const hay = `${row.cardCode} ${row.cardName} ${row.note ?? ""}`.toLowerCase();
      if (!hay.includes(q)) return false;
    }
    if (status && row.status.toLowerCase() !== status) return false;
    return true;
  });

  const published = all.filter((r) => r.status === "published").length;
  const draft = all.filter((r) => r.status === "draft").length;

  const kpis: StatCardModel[] = [
    { key: "all", label: "Tổng phiên bản", value: String(all.length), tone: "default" },
    { key: "pub", label: "Đã Published", value: String(published), tone: "success" },
    { key: "draft", label: "Nháp / phụ lục", value: String(draft), tone: "warning" },
    {
      key: "hint",
      label: "Cách tạo phụ lục",
      value: "Phiên bản mới",
      tone: "info",
      hint: "Không sửa Published",
    },
  ];

  return (
    <AppShell terms={terms} active="rate-cards">
      <section className="panel panel-wide">
        <ListPageHeader
          breadcrumbs={[
            { href: "/dashboard", label: "Trang chủ" },
            { href: "/rate-cards", label: "Bảng giá & Tính giá" },
            { label: "Quản lý phụ lục giá" },
          ]}
          title="Quản lý phụ lục giá"
          lede="Phụ lục = phiên bản mới (amendment). Rate Version đã Published không sửa trực tiếp — lịch sử Rating giữ nguyên."
          action={
            <Link className="btn btn-primary" href="/rate-cards/new">
              ＋ Tạo bảng giá / phiên bản mới
            </Link>
          }
        />

        <StatCardGrid cards={kpis} />

        <FilterBar
          action="/rate-cards/appendices"
          resetHref={sp.q || sp.status ? "/rate-cards/appendices" : undefined}
          fields={[
            {
              kind: "search",
              name: "q",
              label: "Tìm kiếm",
              placeholder: "Mã bảng giá, ghi chú…",
              defaultValue: sp.q ?? "",
            },
            {
              kind: "select",
              name: "status",
              label: "Trạng thái",
              defaultValue: sp.status ?? "",
              options: [
                { value: "", label: "Tất cả" },
                { value: "published", label: "Đã Published" },
                { value: "draft", label: "Nháp" },
              ],
            },
          ]}
        />

        <div className="note" style={{ marginBottom: "1rem" }}>
          <strong>Quy trình phụ lục:</strong> mở bảng giá → tạo phiên bản nháp (điều chỉnh phạm vi /
          điều kiện) → phát hành. Phiên bản cũ Published giữ nguyên cho Rating đã chạy.
        </div>

        {!rows.ok ? (
          <div className="alert alert-error" role="alert">
            {rows.message}
          </div>
        ) : filtered.length === 0 ? (
          <div className="empty-state" role="status">
            {all.length === 0
              ? "Chưa có phiên bản bảng giá."
              : "Không khớp bộ lọc hiện tại."}
          </div>
        ) : (
          <div className="table-wrap">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Mã phụ lục</th>
                  <th>Rate Card</th>
                  <th>Nội dung</th>
                  <th>Hiệu lực từ</th>
                  <th>Phiên bản</th>
                  <th>Trạng thái</th>
                </tr>
              </thead>
              <tbody>
                {filtered.map((row) => (
                  <tr key={row.id}>
                    <td>
                      <Link href={`/rate-cards/${row.rateCardId}`}>
                        {row.cardCode}-v{row.versionNo}
                      </Link>
                    </td>
                    <td>{row.cardCode}</td>
                    <td>{row.note || row.cardName}</td>
                    <td>{formatDateVi(row.effectiveFrom)}</td>
                    <td>v{row.versionNo}</td>
                    <td>{statusLabel(row.status)}</td>
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
