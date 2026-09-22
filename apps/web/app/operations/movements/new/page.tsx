import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { CreateMovementForm } from "@/components/CreateMovementForm";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology } from "@/lib/api";

export default async function NewMovementPage() {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) redirect("/login");
  const terms = await fetchTerminology();

  return (
    <AppShell terms={terms} active="bills" navChild="operations">
      <section className="panel panel-wide">
        <CreateMovementForm />
      </section>
    </AppShell>
  );
}
