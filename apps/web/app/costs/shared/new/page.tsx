import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { CreateSharedCostForm } from "@/components/CreateSharedCostForm";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";

export default async function NewSharedCostPage() {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const terms = await fetchTerminology();
  const costLabel = term(terms, "COST", "Chi phí");
  const sharedLabel = term(terms, "ATTRIBUTION_SHARED", "Chung");

  return (
    <AppShell terms={terms} active="costs">
      <section className="panel">
        <p className="meta-line">
          <Link className="row-link" href="/costs/shared">
            ← {costLabel} {sharedLabel.toLowerCase()}
          </Link>
        </p>
        <h1>
          Tạo {costLabel.toLowerCase()} {sharedLabel.toLowerCase()}
        </h1>
        <CreateSharedCostForm terms={terms} />
      </section>
    </AppShell>
  );
}
