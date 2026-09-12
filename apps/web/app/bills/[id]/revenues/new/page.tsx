import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { CreateRevenueForm } from "@/components/CreateRevenueForm";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import { getBill } from "@/lib/bills";

type Params = Promise<{ id: string }>;

export default async function NewRevenueOnBillPage({
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
  const revenueLabel = term(terms, "REVENUE", "Doanh thu");
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
          Tạo {revenueLabel.toLowerCase()}
        </p>
        <h1>
          Tạo {revenueLabel.toLowerCase()} — {billRes.data.billNo}
        </h1>
        <CreateRevenueForm terms={terms} billId={id} />
      </section>
    </AppShell>
  );
}
