import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { CreateCostForm } from "@/components/CreateCostForm";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import { getBill } from "@/lib/bills";

type Params = Promise<{ id: string }>;

export default async function NewCostOnBillPage({
  params,
}: {
  params: Params;
}) {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const { id } = await params;
  const terms = await fetchTerminology();
  const billLabel = term(terms, "BILL", "Bill");
  const costLabel = term(terms, "COST", "Chi phí");
  const billRes = await getBill(id);

  if (!billRes.ok) {
    return (
      <AppShell terms={terms} active="bills">
        <section className="panel panel-wide">
          <div className="alert alert-error" role="alert">
            {billRes.message}
          </div>
          <p className="cta-row">
            <Link className="btn btn-ghost" href="/bills">
              Quay lại danh sách
            </Link>
          </p>
        </section>
      </AppShell>
    );
  }

  return (
    <AppShell terms={terms} active="bills">
      <section className="panel panel-wide">
        <p className="breadcrumb">
          <Link href="/bills">{billLabel}</Link>
          {" / "}
          <Link href={`/bills/${id}`}>{billRes.data.billNo}</Link>
          {" / "}
          Tạo {costLabel.toLowerCase()}
        </p>
        <h1>
          Tạo {costLabel.toLowerCase()} — {billRes.data.billNo}
        </h1>
        <CreateCostForm terms={terms} billId={id} />
      </section>
    </AppShell>
  );
}
