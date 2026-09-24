import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { AllocateCashForm } from "@/components/AllocateCashForm";
import { FinalizeAllocationButton } from "@/components/FinalizeAllocationButton";
import { ReverseAllocationButton } from "@/components/ReverseAllocationButton";
import { SettlementAllocationTimeline } from "@/components/SettlementAllocationTimeline";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import {
  isOutstanding,
  listAccountsReceivable,
} from "@/lib/ap-ar";
import {
  canReverseAllocation,
  getCollection,
  isDraftAllocation,
  settlementBillLinkLabel,
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
          {collectionLabel} ≠ {revenueLabel}. Chốt phân bổ nháp; đảo khi cần trả
          outstanding.
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
                  {settlementBillLinkLabel(
                    collection.billId,
                    collection.billNo,
                    billLabel
                  )}
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
        <p className="note">
          Tiến trình: ghi nhận {collectionLabel.toLowerCase()} → phân bổ nháp → chốt
          (mới giảm outstanding {arLabel}). Đảo được khi còn nháp / đã chốt.
        </p>
        <SettlementAllocationTimeline
          terms={terms}
          cashLabel={collectionLabel}
          targetLabel={arLabel}
          valueDate={collection.valueDate}
          cashAmount={collection.amount}
          currencyCode={collection.currencyCode}
          allocations={collection.allocations.map((a) => ({
            id: a.id,
            amount: a.amount,
            currencyCode: a.currencyCode,
            allocationStatus: a.allocationStatus,
            createdAt: a.createdAt,
            finalizedAt: a.finalizedAt,
            reversedAt: a.reversedAt,
            reverseReason: a.reverseReason,
            targetId: a.accountsReceivableId,
          }))}
          renderActions={(a) => (
            <>
              {isDraftAllocation(a.allocationStatus) ? (
                <FinalizeAllocationButton
                  terms={terms}
                  kind="collection"
                  allocationId={a.id}
                  amount={a.amount}
                  currencyCode={a.currencyCode}
                  rowVersion={collection.allocations.find((x) => x.id === a.id)?.rowVersion}
                />
              ) : null}
              {canReverseAllocation(a.allocationStatus) ? (
                <ReverseAllocationButton
                  terms={terms}
                  kind="collection"
                  allocationId={a.id}
                  amount={a.amount}
                  currencyCode={a.currencyCode}
                  allocationStatus={a.allocationStatus}
                />
              ) : null}
            </>
          )}
        />

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
              rowVersion={collection.rowVersion}
            />
          </>
        )}
      </section>
    </AppShell>
  );
}
