import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import {
  listCollections,
  listPayments,
  settlementBillLinkLabel,
} from "@/lib/settlements";
import { formatMoney } from "@/lib/money";

type SearchParams = Promise<{ tab?: string }>;

export default async function SettlementsPage({
  searchParams,
}: {
  searchParams: SearchParams;
}) {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const { tab } = await searchParams;
  const activeTab = tab === "collections" ? "collections" : "payments";

  const terms = await fetchTerminology();
  const paymentLabel = term(terms, "PAYMENT", "Thanh toán");
  const collectionLabel = term(terms, "COLLECTION", "Thu tiền");
  const unappliedLabel = term(terms, "UNAPPLIED_AMOUNT", "Chưa áp dụng");
  const availableLabel = term(
    terms,
    "AVAILABLE_TO_ALLOCATE",
    "Còn phân bổ được"
  );
  const costLabel = term(terms, "COST", "Chi phí");
  const revenueLabel = term(terms, "REVENUE", "Doanh thu");
  const billLabel = term(terms, "BILL", "Bill");

  const [payRes, colRes] = await Promise.all([
    listPayments(),
    listCollections(),
  ]);

  const payments = payRes.ok ? payRes.data : [];
  const collections = colRes.ok ? colRes.data : [];

  return (
    <AppShell terms={terms} active="settlements">
      <section className="panel panel-wide">
        <p className="breadcrumb">
          <Link href="/dashboard">Trang chủ</Link>
          {" / "}
          {paymentLabel} &amp; {collectionLabel}
        </p>
        <h1>
          {paymentLabel} &amp; {collectionLabel}
        </h1>
        <p className="lede">
          {paymentLabel} ≠ {costLabel}; {collectionLabel} ≠ {revenueLabel}. Phân
          bổ nháp rồi <strong>chốt phân bổ</strong> mới giảm outstanding AP/AR.
        </p>

        <div className="search-bar" role="tablist" aria-label="Loại dòng tiền">
          <Link
            className={activeTab === "payments" ? "btn" : "btn btn-ghost"}
            href="/settlements"
            role="tab"
            aria-selected={activeTab === "payments"}
          >
            {paymentLabel}
          </Link>
          <Link
            className={activeTab === "collections" ? "btn" : "btn btn-ghost"}
            href="/settlements?tab=collections"
            role="tab"
            aria-selected={activeTab === "collections"}
          >
            {collectionLabel}
          </Link>
          {activeTab === "payments" ? (
            <Link className="btn" href="/settlements/payments/new">
              Tạo {paymentLabel.toLowerCase()}
            </Link>
          ) : (
            <Link className="btn" href="/settlements/collections/new">
              Tạo {collectionLabel.toLowerCase()}
            </Link>
          )}
        </div>

        {activeTab === "payments" ? (
          <>
            {!payRes.ok ? (
              <div className="alert alert-error" role="alert">
                {payRes.message}
              </div>
            ) : payments.length === 0 ? (
              <div className="empty-state" role="status">
                Chưa có {paymentLabel.toLowerCase()}. Tạo mới rồi phân bổ vào
                AP.
              </div>
            ) : (
              <div className="table-wrap">
                <table className="data-table">
                  <thead>
                    <tr>
                      <th scope="col">Ngày</th>
                      <th scope="col" className="num">
                        Số tiền
                      </th>
                      <th scope="col" className="num">
                        {unappliedLabel}
                      </th>
                      <th scope="col" className="num">
                        {availableLabel}
                      </th>
                      <th scope="col">{billLabel}</th>
                      <th scope="col">Tham chiếu</th>
                    </tr>
                  </thead>
                  <tbody>
                    {payments.map((row) => (
                      <tr key={row.id}>
                        <td>
                          <Link
                            className="row-link"
                            href={`/settlements/payments/${row.id}`}
                          >
                            {row.valueDate}
                          </Link>
                        </td>
                        <td className="num">
                          {formatMoney(row.amount, row.currencyCode)}
                        </td>
                        <td className="num">
                          {formatMoney(row.unappliedAmount, row.currencyCode)}
                        </td>
                        <td className="num">
                          {formatMoney(
                            row.availableToAllocate,
                            row.currencyCode
                          )}
                        </td>
                        <td>
                          {row.billId ? (
                            <Link
                              className="row-link"
                              href={`/bills/${row.billId}`}
                            >
                              {settlementBillLinkLabel(
                                row.billId,
                                row.billNo,
                                billLabel
                              )}
                            </Link>
                          ) : (
                            "—"
                          )}
                        </td>
                        <td>{row.referenceNo ?? "—"}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </>
        ) : (
          <>
            {!colRes.ok ? (
              <div className="alert alert-error" role="alert">
                {colRes.message}
              </div>
            ) : collections.length === 0 ? (
              <div className="empty-state" role="status">
                Chưa có {collectionLabel.toLowerCase()}. Tạo mới rồi phân bổ vào
                AR.
              </div>
            ) : (
              <div className="table-wrap">
                <table className="data-table">
                  <thead>
                    <tr>
                      <th scope="col">Ngày</th>
                      <th scope="col" className="num">
                        Số tiền
                      </th>
                      <th scope="col" className="num">
                        {unappliedLabel}
                      </th>
                      <th scope="col" className="num">
                        {availableLabel}
                      </th>
                      <th scope="col">{billLabel}</th>
                      <th scope="col">Tham chiếu</th>
                    </tr>
                  </thead>
                  <tbody>
                    {collections.map((row) => (
                      <tr key={row.id}>
                        <td>
                          <Link
                            className="row-link"
                            href={`/settlements/collections/${row.id}`}
                          >
                            {row.valueDate}
                          </Link>
                        </td>
                        <td className="num">
                          {formatMoney(row.amount, row.currencyCode)}
                        </td>
                        <td className="num">
                          {formatMoney(row.unappliedAmount, row.currencyCode)}
                        </td>
                        <td className="num">
                          {formatMoney(
                            row.availableToAllocate,
                            row.currencyCode
                          )}
                        </td>
                        <td>
                          {row.billId ? (
                            <Link
                              className="row-link"
                              href={`/bills/${row.billId}`}
                            >
                              {settlementBillLinkLabel(
                                row.billId,
                                row.billNo,
                                billLabel
                              )}
                            </Link>
                          ) : (
                            "—"
                          )}
                        </td>
                        <td>{row.referenceNo ?? "—"}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </>
        )}
      </section>
    </AppShell>
  );
}
