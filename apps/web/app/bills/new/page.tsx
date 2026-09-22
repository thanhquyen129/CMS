import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { CreateBillWorkspaceForm } from "@/components/CreateBillWorkspaceForm";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology } from "@/lib/api";
import { loadCreateFormOptions } from "@/lib/create-form-options";

export default async function NewBillPage() {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) redirect("/login");

  const terms = await fetchTerminology();
  const opts = await loadCreateFormOptions();

  return (
    <AppShell terms={terms} active="bills" navChild="bills-new">
      <section className="panel panel-wide">
        <CreateBillWorkspaceForm
          users={opts.users}
          modes={opts.modes}
          locations={opts.locations}
          canonicalRoutes={opts.canonicalRoutes}
          services={opts.services}
          currencies={opts.currencies}
          vendors={opts.vendors}
          orderHits={opts.orderHits}
          shipmentHits={opts.shipmentHits}
          commodities={opts.commodities}
        />
      </section>
    </AppShell>
  );
}
