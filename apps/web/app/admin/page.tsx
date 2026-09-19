import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { ListPageHeader } from "@/components/list/ListPageHeader";
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
    {
      href: "/admin/fx-rates",
      title: "Tỷ giá ngoại tệ",
      desc: "Tỷ giá theo ngày — không tính lại lịch sử chứng từ.",
    },
    {
      href: "/admin/catalog",
      title: "Loại chi phí / doanh thu / dịch vụ",
      desc: "Danh mục loại dùng trên chi phí, bảng giá và chứng từ.",
    },
  ];

  return (
    <AppShell terms={terms} active="admin">
      <section className="panel panel-wide">
        <ListPageHeader
          breadcrumbs={[
            { href: "/dashboard", label: "Trang chủ" },
            { label: "Danh mục dữ liệu" },
          ]}
          title="Danh mục dữ liệu"
          lede="Master data theo tenant — cô lập dữ liệu doanh nghiệp. Không xóa danh mục tại đây — vô hiệu hoá qua API khi cần."
        />

        <div className="hub-module-tabs" role="navigation" aria-label="Danh mục dữ liệu">
          {links.map((item) => (
            <Link key={item.href} href={item.href} className="hub-module-tab">
              <strong>{item.title}</strong>
              <span>{item.desc}</span>
            </Link>
          ))}
        </div>
      </section>
    </AppShell>
  );
}
