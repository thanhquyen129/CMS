import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology } from "@/lib/api";

export default async function AdminHubPage() {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const terms = await fetchTerminology();

  const links = [
    {
      href: "/admin/access",
      title: "Phân quyền",
      desc: "Vai trò hệ thống, gán thành viên, bật/tắt quyền hành động × phạm vi dữ liệu.",
    },
    {
      href: "/admin/parties",
      title: "Đối tác kinh doanh",
      desc: "Khách hàng / NCC — mã và hồ sơ dùng trên chứng từ, AP/AR, thanh toán.",
    },
    {
      href: "/admin/organizations",
      title: "Đơn vị / Tổ chức",
      desc: "Cây tổ chức phục vụ phạm vi dữ liệu và gán Bill.",
    },
    {
      href: "/admin/currencies",
      title: "Tiền tệ",
      desc: "Danh mục tiền tệ và số lẻ thập phân cho số tiền.",
    },
  ];

  return (
    <AppShell terms={terms} active="admin">
      <section className="panel panel-wide">
        <p className="breadcrumb">
          <Link href="/dashboard">Trang chủ</Link>
          {" / "}
          Danh mục dữ liệu
        </p>
        <h1>Danh mục dữ liệu</h1>
        <p className="lede">
          Master data theo tenant — cô lập dữ liệu doanh nghiệp. Không xóa danh mục tại
          đây — vô hiệu hoá qua API khi cần.
        </p>

        <div className="filter-tabs" role="tablist" aria-label="Danh mục dữ liệu">
          {links.map((item) => (
            <Link key={item.href} href={item.href} role="tab" aria-selected="false">
              {item.title}
            </Link>
          ))}
        </div>

        <div className="card-grid" style={{ marginTop: "1.25rem" }}>
          {links.map((item) => (
            <Link
              key={item.href}
              href={item.href}
              className="panel"
              style={{ textDecoration: "none", display: "block" }}
            >
              <h2 className="section-title">{item.title}</h2>
              <p className="muted">{item.desc}</p>
              <span className="btn btn-ghost btn-sm">Mở →</span>
            </Link>
          ))}
        </div>
      </section>
    </AppShell>
  );
}
