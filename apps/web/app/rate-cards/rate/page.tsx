import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { RateCalculatorForm } from "@/components/RateCalculatorForm";
import { ListPageHeader } from "@/components/list";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology } from "@/lib/api";
import { listBills } from "@/lib/bills";
import { listAppendices } from "@/lib/rate-cards-server";

export default async function RatePage() {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) redirect("/login");
  const terms = await fetchTerminology();
  const [bills, versions] = await Promise.all([
    listBills(undefined, { page: 1, pageSize: 50 }),
    listAppendices(),
  ]);
  const billOptions = bills.ok
    ? bills.data.items.map((b) => ({ id: b.id, label: b.billNo }))
    : [];
  const versionOptions = versions.ok
    ? versions.data
        .filter((v) => v.status === "published")
        .map((v) => ({ id: v.id, label: `${v.cardCode} · v${v.versionNo} · ${v.cardName}` }))
    : [];

  return (
    <AppShell terms={terms} active="rate-cards">
      <section className="panel panel-wide">
        <ListPageHeader
          breadcrumbs={[
            { href: "/dashboard", label: "Trang chủ" },
            { href: "/rate-cards", label: "Bảng giá & Tính giá" },
            { label: "Tính giá" },
          ]}
          title="Tính giá"
          lede="Tính Expected Cost/Expected Revenue từ Operational Reference + Rate Card + Rating Context"
          action={<Link className="btn btn-ghost" href="/rate-cards/history">Lịch sử tính giá</Link>}
        />
        {!bills.ok || !versions.ok ? (
          <div className="alert alert-error" role="alert">
            {(bills.ok ? null : bills.message) || (versions.ok ? null : versions.message)}
          </div>
        ) : (
          <RateCalculatorForm bills={billOptions} versions={versionOptions} />
        )}
      </section>
    </AppShell>
  );
}
