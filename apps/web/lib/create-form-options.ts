import { listAccessUsers } from "@/lib/access";
import { listCatalog } from "@/lib/catalog";
import { catalogToOptions, type CatalogOption } from "@/lib/create-workspace";
import { listCurrencies } from "@/lib/master-data";
import { listBills } from "@/lib/bills";
import { listOrders, listShipments } from "@/lib/operational-refs";
import { listBusinessParties } from "@/lib/parties";
import { listRateCards } from "@/lib/rate-cards-server";
import type { RefHit } from "@/components/RefTagPicker";

export type CreateFormOptions = {
  users: CatalogOption[];
  modes: CatalogOption[];
  locations: CatalogOption[];
  routes: CatalogOption[];
  services: CatalogOption[];
  currencies: CatalogOption[];
  vendors: CatalogOption[];
  rateCards: CatalogOption[];
  orderHits: RefHit[];
  billHits: RefHit[];
  shipmentHits: RefHit[];
};

/** Loads catalog/users/refs for UI-02 create screens. */
export async function loadCreateFormOptions(): Promise<CreateFormOptions> {
  const [
    modes,
    locations,
    routes,
    services,
    users,
    currencies,
    vendors,
    rateCards,
    orders,
    bills,
    shipments,
  ] = await Promise.all([
    listCatalog("transport_mode"),
    listCatalog("location"),
    listCatalog("transport_route"),
    listCatalog("service_type"),
    listAccessUsers(),
    listCurrencies(),
    listBusinessParties({ roleCode: "vendor", isActive: "true" }),
    listRateCards({ active: true, pageSize: 50 }),
    listOrders(),
    listBills(undefined, { page: 1, pageSize: 30 }),
    listShipments(),
  ]);

  return {
    users: users.ok
      ? users.data.filter((u) => u.isActive).map((u) => ({ value: u.id, label: u.displayName || u.email }))
      : [],
    modes: modes.ok ? catalogToOptions(modes.data) : [],
    locations: locations.ok ? catalogToOptions(locations.data) : [],
    routes: routes.ok ? catalogToOptions(routes.data) : [],
    services: services.ok ? catalogToOptions(services.data) : [],
    currencies: currencies.ok
      ? currencies.data.filter((c) => c.isActive).map((c) => ({ value: c.code, label: c.code }))
      : [{ value: "VND", label: "VND" }],
    vendors: vendors.ok
      ? vendors.data.map((v) => ({ value: v.id, label: `${v.code} — ${v.name}` }))
      : [],
    rateCards: rateCards.ok
      ? rateCards.data.items.map((r) => ({ value: r.id, label: `${r.code} — ${r.name}` }))
      : [],
    orderHits: orders.ok
      ? orders.data.slice(0, 20).map((o) => ({ id: o.id, code: o.orderNo, title: o.customerName ?? undefined }))
      : [],
    billHits: bills.ok
      ? bills.data.items.slice(0, 20).map((b) => ({ id: b.id, code: b.billNo, title: b.customerName ?? undefined }))
      : [],
    shipmentHits: shipments.ok
      ? shipments.data.slice(0, 20).map((s) => ({ id: s.id, code: s.shipmentNo, title: s.routeCode ?? undefined }))
      : [],
  };
}
