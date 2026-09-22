import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { CompareRatesForm } from "@/components/CompareRatesForm";
import { ListPageHeader } from "@/components/list";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology } from "@/lib/api";

type SearchParams = Promise<Record<string, string | undefined>>;

export default async function CompareRatesPage({ searchParams }: { searchParams: SearchParams }) {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) redirect("/login");
  const terms = await fetchTerminology();
  const sp = await searchParams;
  const initial: Record<string, string> = {};
  for (const [key, value] of Object.entries(sp)) {
    if (value) initial[key] = value;
  }

  return (
    <AppShell terms={terms} active="rate-cards">
      <section className="panel panel-wide">
        <ListPageHeader
          breadcrumbs={[
            { href: "/dashboard", label: "Trang chủ" },
            { href: "/rate-cards", label: "Bảng giá & Tính giá" },
            { label: "So sánh giá" },
          ]}
          title="So sánh giá"
          lede="So sánh nhiều Rate Card phù hợp trên cùng một Rating Context"
        />
        <CompareRatesForm initial={initial} />
      </section>
    </AppShell>
  );
}
