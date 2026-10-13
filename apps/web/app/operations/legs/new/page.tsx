import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { CreateLegForm } from "@/components/CreateLegForm";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology } from "@/lib/api";
import { catalogToOptions, shipmentModeOptions } from "@/lib/create-workspace";
import { listCatalog } from "@/lib/catalog";
import { listShipments } from "@/lib/operational-refs";
import { listLocations } from "@/lib/reference-masters";

export default async function NewLegPage() {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) redirect("/login");
  const terms = await fetchTerminology();
  const [shipmentsRes, locationsRes, modesRes] = await Promise.all([
    listShipments(),
    listLocations(true),
    listCatalog("transport_mode"),
  ]);

  return (
    <AppShell terms={terms} active="bills" navChild="operations">
      <section className="panel panel-wide">
        <CreateLegForm
          shipments={
            shipmentsRes.ok
              ? shipmentsRes.data.map((s) => ({
                  id: s.id,
                  shipmentNo: s.shipmentNo,
                }))
              : []
          }
          shipmentsError={shipmentsRes.ok ? null : shipmentsRes.message}
          modes={shipmentModeOptions(modesRes.ok ? catalogToOptions(modesRes.data) : [])}
          locations={
            locationsRes.ok
              ? locationsRes.data
                  .filter((l) => l.isActive)
                  .map((l) => ({ value: l.code, label: `${l.code} — ${l.name}` }))
              : []
          }
        />
      </section>
    </AppShell>
  );
}
