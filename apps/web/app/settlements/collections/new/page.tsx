import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { CreateCashTxnForm } from "@/components/CreateCashTxnForm";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";

type SearchParams = Promise<{ billId?: string }>;

export default async function NewCollectionPage({
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
  const collectionLabel = term(terms, "COLLECTION", "Thu tiền");

  return (
    <AppShell terms={terms} active="settlements">
      <section className="panel panel-wide">
        <p className="breadcrumb">
          <Link href="/settlements?tab=collections">{collectionLabel}</Link>
          {" / "}
          Tạo mới
        </p>
        <h1>Tạo {collectionLabel.toLowerCase()}</h1>
        <CreateCashTxnForm
          terms={terms}
          kind="collection"
          defaultBillId={billId}
        />
      </section>
    </AppShell>
  );
}
