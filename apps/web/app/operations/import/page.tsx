import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { ImportOperationalForm } from "@/components/ImportOperationalForm";
import { ListPageHeader } from "@/components/list";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";

export default async function OperationalImportPage() {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) redirect("/login");
  const terms = await fetchTerminology();
  const billLabel = term(terms, "BILL", "Bill");

  return (
    <AppShell terms={terms} active="bills">
      <section className="panel panel-wide">
        <ListPageHeader
          breadcrumbs={[
            { href: "/dashboard", label: "Trang chủ" },
            { href: "/bills", label: `Đơn hàng vận chuyển` },
            { label: "Nhập nghiệp vụ" },
          ]}
          title="Nhập nghiệp vụ"
          lede={`Nhập hàng loạt Order / ${billLabel} / Shipment / Chặng / Chuyến — xem trước rồi ghi tất cả hoặc không ghi gì.`}
        />
        <ImportOperationalForm />
      </section>
    </AppShell>
  );
}
