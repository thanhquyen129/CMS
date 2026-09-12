import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { StartReconciliationForm } from "@/components/StartReconciliationForm";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";

type SearchParams = Promise<{ billId?: string }>;

export default async function NewReconciliationPage({
  searchParams,
}: {
  searchParams: SearchParams;
}) {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const { billId } = await searchParams;
  const terms = await fetchTerminology();
  const reconLabel = term(terms, "RECONCILIATION", "Đối soát");

  return (
    <AppShell terms={terms} active="reconciliations">
      <section className="panel panel-wide">
        <p className="breadcrumb">
          <Link href="/reconciliations">{reconLabel}</Link>
          {" / "}
          Mở phiên mới
        </p>
        <h1>Mở phiên {reconLabel.toLowerCase()}</h1>
        <StartReconciliationForm terms={terms} defaultBillId={billId} />
      </section>
    </AppShell>
  );
}
