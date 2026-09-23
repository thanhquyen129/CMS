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
          lede="Tải file mẫu, điền khách hàng và nhà cung cấp, xem trước từng dòng rồi ghi một lần. Gồm MST, vai trò, hạn mức, tài khoản nhận tiền và người liên hệ."
        />
        <ImportPartiesForm />
      </section>
    </AppShell>
  );
}
