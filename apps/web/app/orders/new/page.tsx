import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { CreateOrderForm } from "@/components/CreateOrderForm";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology } from "@/lib/api";
import { loadCreateFormOptions } from "@/lib/create-form-options";

export default async function NewOrderPage() {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) redirect("/login");
  const terms = await fetchTerminology();
  const opts = await loadCreateFormOptions();

  return (
    <AppShell terms={terms} active="bills" navChild="orders-new">
      <section className="panel panel-wide">
        <CreateOrderForm
          users={opts.users}
          modes={opts.modes}
          locations={opts.locations}
          routes={opts.routes}
          services={opts.services}
          billHits={opts.billHits}
          shipmentHits={opts.shipmentHits}
          commodities={opts.commodities}
        />
      </section>
    </AppShell>
  );
}
