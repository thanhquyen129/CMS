import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { ListPageHeader } from "@/components/list";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology } from "@/lib/api";
import { formatDateVi } from "@/lib/money";
import { listAppendices } from "@/lib/rate-cards-server";

function statusLabel(status: string): string {
  if (status === "published") return "Đã Published";
  if (status === "draft") return "Nháp";
  return status;
}

export default async function AppendicesPage() {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) redirect("/login");
  const terms = await fetchTerminology();
  const rows = await listAppendices();

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
          lede="Quản lý phụ lục/amendment điều chỉnh phạm vi hoặc điều kiện của bảng giá theo phiên bản"
          action={<Link className="btn" href="/rate-cards">＋ Tạo phụ lục giá</Link>}
        />
        <p className="muted">
          Rate Version đã Published không sửa trực tiếp. Phụ lục là phiên bản mới, giữ nguyên lịch sử Rating cũ.
        </p>
        {!rows.ok ? (
          <div className="alert alert-error" role="alert">{rows.message}</div>
        ) : rows.data.length === 0 ? (
          <div className="empty-state">Chưa có phiên bản bảng giá.</div>
        ) : (
          <table className="data-table">
            <thead>
              <tr>
                <th>Mã phụ lục</th>
                <th>Rate Card</th>
                <th>Nội dung</th>
                <th>Hiệu lực từ</th>
                <th>Phiên bản mới</th>
                <th>Trạng thái</th>
              </tr>
            </thead>
            <tbody>
              {rows.data.map((row) => (
                <tr key={row.id}>
                  <td><Link href={`/rate-cards/${row.rateCardId}`}>{row.cardCode}-v{row.versionNo}</Link></td>
                  <td>{row.cardCode}</td>
                  <td>{row.note || row.cardName}</td>
                  <td>{formatDateVi(row.effectiveFrom)}</td>
                  <td>v{row.versionNo}</td>
                  <td>{statusLabel(row.status)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>
    </AppShell>
  );
}
