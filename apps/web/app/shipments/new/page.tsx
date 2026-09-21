import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { CreateShipmentForm } from "@/components/CreateShipmentForm";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology } from "@/lib/api";
import { loadCreateFormOptions } from "@/lib/create-form-options";

export default async function NewShipmentPage() {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) redirect("/login");
  const terms = await fetchTerminology();
  const opts = await loadCreateFormOptions();

  return (
    <AppShell terms={terms} active="bills" navChild="shipments-new">
      <section className="panel panel-wide">
        <CreateShipmentForm
          users={opts.users}
          modes={opts.modes}
          locations={opts.locations}
          services={opts.services}
          currencies={opts.currencies}
          vendors={opts.vendors}
          rateCards={opts.rateCards}
          billHits={opts.billHits}
        />
      </section>
    </AppShell>
  );
}
