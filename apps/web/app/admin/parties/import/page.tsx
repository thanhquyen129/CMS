import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { ImportPartiesForm } from "@/components/ImportPartiesForm";
import { ListPageHeader } from "@/components/list";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";

export default async function PartyImportPage() {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) redirect("/login");
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
            { label: "Nhập đối tác" },
          ]}
          title="Nhập đối tác"
          lede="Nhập hàng loạt đối tác từ CSV — xem trước rồi ghi tất cả hoặc không ghi gì. Không gộp hồ sơ trùng."
        />
        <ImportPartiesForm />
      </section>
    </AppShell>
  );
}
