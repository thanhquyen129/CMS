import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { CreateLegForm } from "@/components/CreateLegForm";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology } from "@/lib/api";
import { listShipments } from "@/lib/operational-refs";

export default async function NewLegPage() {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) redirect("/login");
  const terms = await fetchTerminology();
  const shipmentsRes = await listShipments();

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
        />
      </section>
    </AppShell>
  );
}
