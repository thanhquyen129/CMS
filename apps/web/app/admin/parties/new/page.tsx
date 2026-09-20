import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { CreateBusinessPartyForm } from "@/components/CreateBusinessPartyForm";
import { ListPageHeader } from "@/components/list";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";

export default async function NewBusinessPartyPage() {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const terms = await fetchTerminology();
  const dashboardLabel = term(terms, "DASHBOARD", "Bảng điều khiển");

  return (
    <AppShell terms={terms} active="admin">
      <section className="panel panel-wide">
        <ListPageHeader
          breadcrumbs={[
            { href: "/dashboard", label: dashboardLabel },
            { href: "/admin", label: "Danh mục" },
            { href: "/admin/parties", label: "Đối tác" },
            { label: "Thêm mới" },
          ]}
          title="Thêm đối tác kinh doanh"
          lede="Nhập hồ sơ pháp lý và điều khoản tài chính. Sau khi lưu, bổ sung tài khoản ngân hàng và người liên hệ trên hồ sơ."
          action={
            <Link className="btn btn-ghost" href="/admin/parties">
              ← Danh sách
            </Link>
          }
        />
        <CreateBusinessPartyForm />
      </section>
    </AppShell>
  );
}
