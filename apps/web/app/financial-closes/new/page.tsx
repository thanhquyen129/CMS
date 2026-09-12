import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { StartFinancialCloseForm } from "@/components/StartFinancialCloseForm";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";

type SearchParams = Promise<{ billId?: string; scopeType?: string }>;

export default async function NewFinancialClosePage({
  searchParams,
}: {
  searchParams: SearchParams;
}) {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const sp = await searchParams;
  const terms = await fetchTerminology();
  const closeLabel = term(terms, "FINANCIAL_CLOSE", "Chốt tài chính");
  const scopeType = sp.billId || sp.scopeType === "bill" ? "bill" : "period";

  return (
    <AppShell terms={terms} active="financial-closes">
      <section className="panel panel-wide">
        <p className="breadcrumb">
          <Link href="/financial-closes">{closeLabel}</Link>
          {" / "}
          Mở mới
        </p>
        <h1>Mở {closeLabel.toLowerCase()}</h1>
        <StartFinancialCloseForm
          terms={terms}
          defaultScopeType={scopeType}
          defaultScopeId={sp.billId}
        />
      </section>
    </AppShell>
  );
}
