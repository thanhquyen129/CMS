import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { AllocateCashForm } from "@/components/AllocateCashForm";
import { FinalizeAllocationButton } from "@/components/FinalizeAllocationButton";
import { CancelCashButton } from "@/components/CancelCashButton";
import { ReverseAllocationButton } from "@/components/ReverseAllocationButton";
import { SettlementAllocationTimeline } from "@/components/SettlementAllocationTimeline";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import {
  isOutstanding,
  listAccountsPayable,
} from "@/lib/ap-ar";
import {
  canReverseAllocation,
  getPayment,
  isDraftAllocation,
  isFinalizedAllocation,
  settlementBillLinkLabel,
} from "@/lib/settlements";
import { formatMoney } from "@/lib/money";

type Params = Promise<{ id: string }>;

export default async function PaymentDetailPage({
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
  const paymentLabel = term(terms, "PAYMENT", "Thanh toán");
  const apLabel = term(terms, "ACCOUNTS_PAYABLE", "Khoản phải trả");
  const billLabel = term(terms, "BILL", "Bill");
  const unappliedLabel = term(terms, "UNAPPLIED_AMOUNT", "Chưa áp dụng");
  const availableLabel = term(
    terms,
    "AVAILABLE_TO_ALLOCATE",
    "Còn phân bổ được"
  );
  const costLabel = term(terms, "COST", "Chi phí");

  const [payRes, apRes] = await Promise.all([
    getPayment(id),
    listAccountsPayable(),
  ]);

  if (!payRes.ok) {
    return (
      <AppShell terms={terms} active="settlements">
        <section className="panel panel-wide">
          <div className="alert alert-error" role="alert">
            {payRes.message}
          </div>
          <Link className="btn btn-ghost" href="/settlements">
            Quay lại
          </Link>
        </section>
      </AppShell>
    );
  }

  const payment = payRes.data;
  const apItems = apRes.ok ? apRes.data.filter(isOutstanding) : [];
  const targets = apItems
    .filter(
      (a) =>
        a.currencyCode.toUpperCase() === payment.currencyCode.toUpperCase() &&
        (!payment.billId || a.billId === payment.billId || !a.billId)
    )
    .map((a) => ({
      id: a.id,
      label: a.billId
        ? `${apLabel} · Bill ${a.billId.slice(0, 8)}…`
        : `${apLabel} · ${a.id.slice(0, 8)}…`,
      outstanding: a.outstanding,
      currencyCode: a.currencyCode,
    }));

  const draftAllocs = payment.allocations.filter((a) =>
    isDraftAllocation(a.allocationStatus)
  );
  const openPayment = payment.status.toLowerCase() === "open";
  const hasFinalized = payment.allocations.some((a) =>
    isFinalizedAllocation(a.allocationStatus)
  );

  return (
    <AppShell terms={terms} active="settlements">
      <section className="panel panel-wide">
        <p className="breadcrumb">
          <Link href="/settlements">{paymentLabel}</Link>
          {" / "}
          Chi tiết
        </p>
        <h1>
          {paymentLabel} · {formatMoney(payment.amount, payment.currencyCode)}
        </h1>
        <p className="lede">
          {paymentLabel} ≠ {costLabel}. Chốt phân bổ nháp; đảo khi cần trả
          outstanding.
        </p>
        {openPayment && !hasFinalized ? (
          <CancelCashButton kind="payment" cashId={payment.id} />
        ) : null}
        {!openPayment ? (
          <p className="note">Thanh toán đã hủy. Số tiền giữ trong audit.</p>
        ) : null}
        {openPayment && hasFinalized ? (
          <p className="note">
            Còn phân bổ đã chốt — đảo phân bổ trước khi hủy thanh toán.
          </p>
        ) : null}

        <dl className="metric-grid">
          <div>
            <dt>Ngày giá trị</dt>
            <dd>{payment.valueDate}</dd>
          </div>
          <div>
            <dt>{unappliedLabel}</dt>
            <dd>
              {formatMoney(payment.unappliedAmount, payment.currencyCode)}
            </dd>
          </div>
          <div>
            <dt>{availableLabel}</dt>
            <dd>
              {formatMoney(payment.availableToAllocate, payment.currencyCode)}
            </dd>
          </div>
          <div>
            <dt>{billLabel}</dt>
            <dd>
              {payment.billId ? (
                <Link className="row-link" href={`/bills/${payment.billId}`}>
                  {settlementBillLinkLabel(
                    payment.billId,
                    payment.billNo,
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
            <dd>{payment.referenceNo ?? "—"}</dd>
          </div>
        </dl>

        <h2 className="section-title">Phân bổ</h2>
        <p className="note">
          Tiến trình: ghi nhận {paymentLabel.toLowerCase()} → phân bổ nháp → chốt
          (mới giảm outstanding {apLabel}). Đảo được khi còn nháp / đã chốt.
        </p>
        <SettlementAllocationTimeline
          terms={terms}
          cashLabel={paymentLabel}
          targetLabel={apLabel}
          valueDate={payment.valueDate}
          cashAmount={payment.amount}
          currencyCode={payment.currencyCode}
          allocations={payment.allocations.map((a) => ({
            id: a.id,
            amount: a.amount,
            currencyCode: a.currencyCode,
            allocationStatus: a.allocationStatus,
            createdAt: a.createdAt,
            finalizedAt: a.finalizedAt,
            reversedAt: a.reversedAt,
            reverseReason: a.reverseReason,
            targetId: a.accountsPayableId,
          }))}
          renderActions={(a) => (
            <>
              {isDraftAllocation(a.allocationStatus) ? (
                <FinalizeAllocationButton
                  terms={terms}
                  kind="payment"
                  allocationId={a.id}
                  amount={a.amount}
                  currencyCode={a.currencyCode}
                  rowVersion={payment.allocations.find((x) => x.id === a.id)?.rowVersion}
                />
              ) : null}
              {canReverseAllocation(a.allocationStatus) ? (
                <ReverseAllocationButton
                  terms={terms}
                  kind="payment"
                  allocationId={a.id}
                  amount={a.amount}
                  currencyCode={a.currencyCode}
                  allocationStatus={a.allocationStatus}
                  rowVersion={payment.allocations.find((x) => x.id === a.id)?.rowVersion}
                />
              ) : null}
            </>
          )}
        />

        {draftAllocs.length > 0 ? (
          <p className="note">
            Có {draftAllocs.length} phân bổ nháp — chốt để giảm outstanding{" "}
            {apLabel}.
          </p>
        ) : null}

        {!openPayment ? null : !apRes.ok ? (
          <div className="alert alert-error" role="alert">
            {apRes.message}
          </div>
        ) : (
          <>
            <h2 className="section-title sm">Tạo phân bổ nháp</h2>
            <AllocateCashForm
              terms={terms}
              kind="payment"
              cashId={payment.id}
              availableToAllocate={payment.availableToAllocate}
              currencyCode={payment.currencyCode}
              targets={targets}
              rowVersion={payment.rowVersion}
            />
          </>
        )}
      </section>
    </AppShell>
  );
}
