import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { RateCardDeclarationForm } from "@/components/RateCardDeclarationForm";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology } from "@/lib/api";
import { listCatalog } from "@/lib/catalog";
import { listCurrencies } from "@/lib/master-data";
import { listCommodities, listRoutes } from "@/lib/reference-masters";
import { getTenantProfile } from "@/lib/tenant-admin";

export default async function NewRateCardPage() {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const [terms, routes, currencies, profile, commodities, services, modes, costs, revenues] = await Promise.all([
    fetchTerminology(),
    listRoutes(false),
    listCurrencies(),
    getTenantProfile(),
    listCommodities(true),
    listCatalog("service_type"),
    listCatalog("transport_mode"),
    listCatalog("cost_type"),
    listCatalog("revenue_type"),
  ]);
  const active = currencies.ok ? currencies.data.filter((c) => c.isActive) : [];
  const preferred = (profile.ok ? profile.data.defaultCurrencyCode : "VND").toUpperCase();
  const defaultCurrency = active.some((c) => c.code.toUpperCase() === preferred)
    ? preferred
    : active.some((c) => c.code.toUpperCase() === "VND")
      ? "VND"
      : active[0]?.code ?? preferred;

  return (
    <AppShell terms={terms} active="rate-cards">
      <section className="panel panel-wide">
        <p className="meta-line">
          <Link className="row-link" href="/rate-cards">
            ← Bảng giá
          </Link>
        </p>
        <h1>Tạo bảng giá</h1>
        <RateCardDeclarationForm
          defaultCurrency={defaultCurrency}
          currencies={currencies.ok ? currencies.data : []}
          routes={routes.ok ? routes.data : []}
          commodities={commodities.ok ? commodities.data : []}
          serviceTypes={services.ok ? services.data : []}
          transportModes={modes.ok ? modes.data : []}
          costTypes={costs.ok ? costs.data : []}
          revenueTypes={revenues.ok ? revenues.data : []}
          routeError={routes.ok ? null : routes.message}
          currencyError={currencies.ok ? null : currencies.message}
        />
      </section>
    </AppShell>
  );
}
