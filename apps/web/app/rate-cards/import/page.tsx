import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { ImportRateCardsForm } from "@/components/ImportRateCardsForm";
import { ListPageHeader } from "@/components/list";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology } from "@/lib/api";

export default async function RateImportPage() {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) redirect("/login");
  const terms = await fetchTerminology();

  return (
    <AppShell terms={terms} active="rate-cards">
      <section className="panel panel-wide">
        <ListPageHeader
          breadcrumbs={[
            { href: "/dashboard", label: "Trang chủ" },
            { href: "/rate-cards", label: "Bảng giá & Tính giá" },
            { label: "Nhập bảng giá" },
          ]}
          title="Nhập bảng giá"
          lede="Nhập hàng loạt bảng giá (JSON). Phiên bản mới ở trạng thái nháp; Published bất biến."
        />
        <ImportRateCardsForm />
      </section>
    </AppShell>
  );
}
