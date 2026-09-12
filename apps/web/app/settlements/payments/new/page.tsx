import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { CreateCashTxnForm } from "@/components/CreateCashTxnForm";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";

type SearchParams = Promise<{ billId?: string }>;

export default async function NewPaymentPage({
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
  const paymentLabel = term(terms, "PAYMENT", "Thanh toán");

  return (
    <AppShell terms={terms} active="settlements">
      <section className="panel panel-wide">
        <p className="breadcrumb">
          <Link href="/settlements">{paymentLabel}</Link>
          {" / "}
          Tạo mới
        </p>
        <h1>Tạo {paymentLabel.toLowerCase()}</h1>
        <CreateCashTxnForm
          terms={terms}
          kind="payment"
          defaultBillId={billId}
        />
      </section>
    </AppShell>
  );
}
