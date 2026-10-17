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
import { getRateCard, listEconomicChargeTypes, listPricingRules, listRateVersions } from "@/lib/rate-cards-server";
import { isDraftVersion, isWeightBreakMethod } from "@/lib/rate-cards";

type Params = Promise<{ id: string }>;

export default async function EditRateCardPage({ params }: { params: Params }) {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) redirect("/login");

  const { id } = await params;
  const [terms, cardRes, versionsRes, routes, currencies, commodities, services, modes, costs, revenues, charges] = await Promise.all([
    fetchTerminology(),
    getRateCard(id),
    listRateVersions(id),
    listRoutes(false),
    listCurrencies(),
    listCommodities(true),
    listCatalog("service_type"),
    listCatalog("transport_mode"),
    listCatalog("cost_type"),
    listCatalog("revenue_type"),
    listEconomicChargeTypes(),
  ]);

  if (!cardRes.ok) {
    return (
      <AppShell terms={terms} active="rate-cards">
        <section className="panel">
          <h1>Bảng giá</h1>
          <div className="alert alert-error" role="alert">{cardRes.message}</div>
          <Link className="btn btn-ghost" href="/rate-cards">Quay lại</Link>
        </section>
      </AppShell>
    );
  }

  const versions = versionsRes.ok ? versionsRes.data : [];
  const draft = versions.find((v) => isDraftVersion(v.status));
  if (!draft) {
    return (
      <AppShell terms={terms} active="rate-cards">
        <section className="panel">
          <h1>Chỉnh sửa bảng giá</h1>
          <p className="note">Phiên bản đã phát hành không sửa trực tiếp. Hãy tạo phiên bản mới.</p>
          <Link className="btn" href={`/rate-cards/${id}`}>Về chi tiết</Link>
        </section>
      </AppShell>
    );
  }

  const rulesRes = await listPricingRules(draft.id);
  const rules = rulesRes.ok ? rulesRes.data : [];
  const card = cardRes.data;
  const inferred = card.declarationMode
    ?? (rules.some((r) => isWeightBreakMethod(r.calcMethod)) ? "advanced" : rules.length > 0 ? "basic" : "basic");

  return (
    <AppShell terms={terms} active="rate-cards">
      <section className="panel panel-wide">
        <p className="meta-line">
          <Link className="row-link" href={`/rate-cards/${id}`}>← {card.code}</Link>
        </p>
        <h1>Chỉnh sửa bảng giá</h1>
        <RateCardDeclarationForm
          cardId={card.id}
          code={card.code}
          name={card.name}
          partyType={card.partyType}
          currencyCode={card.currencyCode}
          description={card.description}
          carrierName={card.carrierName}
          routeId={card.routeId}
          routeCode={card.routeCode}
          supplierPartyId={card.supplierPartyId}
          customerPartyId={card.customerPartyId}
          customerGroupCode={card.customerGroupCode}
          declarationMode={inferred}
          modeLocked={Boolean(card.hasPublishedVersion)}
          versionId={draft.id}
          vatRate={draft.vatRate}
          effectiveFrom={draft.effectiveFrom}
          note={draft.note}
          transportMode={card.transportMode}
          rules={rules}
          rowVersion={card.rowVersion}
          defaultCurrency={card.currencyCode}
          currencies={currencies.ok ? currencies.data : []}
          routes={routes.ok ? routes.data : []}
          commodities={commodities.ok ? commodities.data : []}
          serviceTypes={services.ok ? services.data : []}
          transportModes={modes.ok ? modes.data : []}
          costTypes={costs.ok ? costs.data : []}
          revenueTypes={revenues.ok ? revenues.data : []}
          chargeItems={charges.ok ? charges.data : []}
          routeError={routes.ok ? null : routes.message}
          currencyError={currencies.ok ? null : currencies.message}
        />
      </section>
    </AppShell>
  );
}
