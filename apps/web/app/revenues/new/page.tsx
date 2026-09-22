import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { CreateRevenueForm } from "@/components/CreateRevenueForm";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import { listBills } from "@/lib/bills";

type SearchParams = Promise<{ billId?: string }>;

export default async function NewRevenuePage({
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
  const billLabel = term(terms, "BILL", "Bill");
  const bills = await listBills(undefined, { page: 1, pageSize: 50 });

  return (
    <AppShell terms={terms} active="revenues">
      <section className="panel panel-wide">
        <p className="meta-line">
          <Link href="/revenues">Danh sách doanh thu</Link>
          <span aria-hidden="true"> / </span>
          <span>Tạo doanh thu</span>
        </p>
        <h1>Tạo doanh thu</h1>
        <p className="lede">Doanh thu kinh tế gắn một {billLabel}. Chia nhiều {billLabel} sau khi ghi.</p>
        {!bills.ok ? (
          <div className="alert alert-error" role="alert">{bills.message}</div>
        ) : (
          <ul className="inline-list">
            {bills.data.items.map((bill) => (
              <li key={bill.id}>
                <Link className={bill.id === billId ? "row-link" : undefined} href={`/revenues/new?billId=${bill.id}`}>
                  {bill.billNo}
                </Link>
              </li>
            ))}
          </ul>
        )}
        {billId ? <CreateRevenueForm terms={terms} billId={billId} /> : <p className="note">Chọn {billLabel} trước khi nhập số tiền.</p>}
      </section>
    </AppShell>
  );
}
