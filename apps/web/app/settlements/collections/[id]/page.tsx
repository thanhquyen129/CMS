import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { AllocateCashForm } from "@/components/AllocateCashForm";
import { FinalizeAllocationButton } from "@/components/FinalizeAllocationButton";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import {
  isOutstanding,
  listAccountsReceivable,
} from "@/lib/ap-ar";
import {
  allocationStatusLabel,
  getCollection,
  isDraftAllocation,
} from "@/lib/settlements";
import { formatMoney } from "@/lib/money";

type Params = Promise<{ id: string }>;

export default async function CollectionDetailPage({
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
  const collectionLabel = term(terms, "COLLECTION", "Thu tiền");
  const arLabel = term(terms, "ACCOUNTS_RECEIVABLE", "Khoản phải thu");
  const billLabel = term(terms, "BILL", "Bill");
  const unappliedLabel = term(terms, "UNAPPLIED_AMOUNT", "Chưa áp dụng");
  const availableLabel = term(
    terms,
    "AVAILABLE_TO_ALLOCATE",
    "Còn phân bổ được"
  );
  const revenueLabel = term(terms, "REVENUE", "Doanh thu");

  const [colRes, arRes] = await Promise.all([
    getCollection(id),
    listAccountsReceivable(),
  ]);

  if (!colRes.ok) {
    return (
      <AppShell terms={terms} active="settlements">
        <section className="panel panel-wide">
          <div className="alert alert-error" role="alert">
            {colRes.message}
          </div>
          <Link className="btn btn-ghost" href="/settlements?tab=collections">
            Quay lại
          </Link>
        </section>
      </AppShell>
    );
  }

  const collection = colRes.data;
  const arItems = arRes.ok ? arRes.data.filter(isOutstanding) : [];
  const targets = arItems
    .filter(
      (a) =>
        a.currencyCode.toUpperCase() ===
          collection.currencyCode.toUpperCase() &&
        (!collection.billId || a.billId === collection.billId || !a.billId)
    )
    .map((a) => ({
      id: a.id,
      label: a.billId
        ? `${arLabel} · Bill ${a.billId.slice(0, 8)}…`
        : `${arLabel} · ${a.id.slice(0, 8)}…`,
      outstanding: a.outstanding,
      currencyCode: a.currencyCode,
    }));

  const draftAllocs = collection.allocations.filter((a) =>
    isDraftAllocation(a.allocationStatus)
  );

  return (
    <AppShell terms={terms} active="settlements">
      <section className="panel panel-wide">
        <p className="breadcrumb">
          <Link href="/settlements?tab=collections">{collectionLabel}</Link>
          {" / "}
          Chi tiết
        </p>
        <h1>
          {collectionLabel} ·{" "}
          {formatMoney(collection.amount, collection.currencyCode)}
        </h1>
        <p className="lede">
          {collectionLabel} ≠ {revenueLabel}. Hành động chính:{" "}
          <strong>chốt phân bổ</strong> khi đã có dòng nháp.
        </p>

        <dl className="metric-grid">
          <div>
            <dt>Ngày giá trị</dt>
            <dd>{collection.valueDate}</dd>
          </div>
          <div>
            <dt>{unappliedLabel}</dt>
            <dd>
              {formatMoney(
                collection.unappliedAmount,
                collection.currencyCode
              )}
            </dd>
          </div>
          <div>
            <dt>{availableLabel}</dt>
            <dd>
              {formatMoney(
                collection.availableToAllocate,
                collection.currencyCode
              )}
            </dd>
          </div>
          <div>
            <dt>{billLabel}</dt>
            <dd>
              {collection.billId ? (
                <Link
                  className="row-link"
                  href={`/bills/${collection.billId}`}
                >
                  Mở {billLabel}
                </Link>
              ) : (
                "—"
              )}
            </dd>
          </div>
          <div>
            <dt>Tham chiếu</dt>
            <dd>{collection.referenceNo ?? "—"}</dd>
          </div>
        </dl>

        <h2 className="section-title">Phân bổ</h2>
        {collection.allocations.length === 0 ? (
          <div className="empty-state" role="status">
            Chưa có phân bổ. Tạo nháp bên dưới.
          </div>
        ) : (
          <div className="table-wrap">
            <table className="data-table">
              <thead>
                <tr>
                  <th scope="col">{arLabel}</th>
                  <th scope="col" className="num">
                    Số tiền
                  </th>
                  <th scope="col">Trạng thái</th>
                  <th scope="col">Thao tác</th>
                </tr>
              </thead>
              <tbody>
                {collection.allocations.map((a) => (
                  <tr key={a.id}>
                    <td>
                      <code className="mono-id">
                        {a.accountsReceivableId.slice(0, 8)}…
                      </code>
                    </td>
                    <td className="num">
                      {formatMoney(a.amount, a.currencyCode)}
                    </td>
                    <td>
                      {allocationStatusLabel(terms, a.allocationStatus)}
                    </td>
                    <td>
                      {isDraftAllocation(a.allocationStatus) ? (
                        <FinalizeAllocationButton
                          terms={terms}
                          kind="collection"
                          allocationId={a.id}
                          amount={a.amount}
                          currencyCode={a.currencyCode}
                        />
                      ) : (
                        "—"
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {draftAllocs.length > 0 ? (
          <p className="note">
            Có {draftAllocs.length} phân bổ nháp — chốt để giảm outstanding{" "}
            {arLabel}.
          </p>
        ) : null}

        {!arRes.ok ? (
          <div className="alert alert-error" role="alert">
            {arRes.message}
          </div>
        ) : (
          <>
            <h2 className="section-title sm">Tạo phân bổ nháp</h2>
            <AllocateCashForm
              terms={terms}
              kind="collection"
              cashId={collection.id}
              availableToAllocate={collection.availableToAllocate}
              currencyCode={collection.currencyCode}
              targets={targets}
            />
          </>
        )}
      </section>
    </AppShell>
  );
}
