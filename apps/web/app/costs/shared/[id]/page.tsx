import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { AdjustCostRevenueButton } from "@/components/AdjustCostRevenueButton";
import { AdjustmentHistoryTable } from "@/components/AdjustmentHistoryTable";
import { AllocateSharedCostForm } from "@/components/AllocateSharedCostForm";
import { FinalizeCostAllocationButton } from "@/components/FinalizeCostAllocationButton";
import { MaturityTransitionButton } from "@/components/MaturityTransitionButton";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import { listBills } from "@/lib/bills";
import { getCost } from "@/lib/costs-revenues-server";
import {
  allocationBasisLabel,
  allocationStatusLabel,
  canActualize,
  canConfirm,
  isSharedCost,
  maturityLabelKey,
} from "@/lib/costs-revenues";
import { formatDateTimeVi, formatMoney } from "@/lib/money";

type Params = Promise<{ id: string }>;

export default async function SharedCostDetailPage({
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
  const costLabel = term(terms, "COST", "Chi phí");
  const sharedLabel = term(terms, "ATTRIBUTION_SHARED", "Chung");
  const billLabel = term(terms, "BILL", "Bill");
  const allocLabel = term(terms, "COST_ALLOCATION", "Phân bổ chi phí");
  const expected = term(terms, "EXPECTED", "Dự kiến");
  const confirmed = term(terms, "CONFIRMED", "Đã xác nhận");
  const actual = term(terms, "ACTUAL", "Thực tế");
  const adjLabel = term(terms, "ADJUSTMENT", "Điều chỉnh");

  const [costRes, billsRes] = await Promise.all([getCost(id), listBills()]);

  if (!costRes.ok) {
    return (
      <AppShell terms={terms} active="costs">
        <section className="panel">
          <p className="meta-line">
            <Link className="row-link" href="/costs/shared">
              ← {costLabel} {sharedLabel.toLowerCase()}
            </Link>
          </p>
          <div className="alert alert-error" role="alert">
            {costRes.message}
          </div>
        </section>
      </AppShell>
    );
  }

  const cost = costRes.data;
  if (!isSharedCost(cost.attributionType)) {
    return (
      <AppShell terms={terms} active="costs">
        <section className="panel">
          <p className="meta-line">
            <Link className="row-link" href="/costs/shared">
              ← {costLabel} {sharedLabel.toLowerCase()}
            </Link>
          </p>
          <div className="alert alert-error" role="alert">
            Đây không phải {costLabel.toLowerCase()} {sharedLabel.toLowerCase()}
            .
            {cost.billId ? (
              <>
                {" "}
                <Link className="row-link" href={`/bills/${cost.billId}`}>
                  Mở {billLabel} gắn trực tiếp
                </Link>
              </>
            ) : null}
          </div>
        </section>
      </AppShell>
    );
  }

  const maturityKey = maturityLabelKey(cost.financialMaturity);
  const maturityVi =
    maturityKey === "EXPECTED"
      ? expected
      : maturityKey === "CONFIRMED"
        ? confirmed
        : maturityKey === "ACTUAL"
          ? actual
          : cost.financialMaturity;

  const bills = billsRes.ok ? billsRes.data : [];
  const billNoById = new Map(bills.map((b) => [b.id, b.billNo]));
  const allocations = [...(cost.allocations ?? [])].sort(
    (a, b) => b.versionNo - a.versionNo
  );
  const hasDraft = allocations.some(
    (a) => a.allocationStatus?.toLowerCase() === "draft"
  );
  const draft = allocations.find(
    (a) => a.allocationStatus?.toLowerCase() === "draft"
  );

  return (
    <AppShell terms={terms} active="costs">
      <section className="panel panel-wide">
        <p className="meta-line">
          <Link className="row-link" href="/costs/shared">
            ← {costLabel} {sharedLabel.toLowerCase()}
          </Link>
        </p>
        <h1>
          {cost.costTypeCode || `${costLabel} ${cost.id.slice(0, 8)}`}
        </h1>
        <p className="lede">
          {sharedLabel} · {maturityVi} ·{" "}
          <strong>{formatMoney(cost.amount, cost.currencyCode)}</strong>
          {cost.recordStatus !== "active" ? ` · ${cost.recordStatus}` : ""}
        </p>

        <dl className="metric-grid">
          <div>
            <dt>{expected}</dt>
            <dd>{formatMoney(cost.expectedAmount, cost.currencyCode)}</dd>
          </div>
          <div>
            <dt>{confirmed}</dt>
            <dd>
              {cost.confirmedAmount != null
                ? formatMoney(cost.confirmedAmount, cost.currencyCode)
                : "—"}
            </dd>
          </div>
          <div>
            <dt>{actual}</dt>
            <dd>
              {cost.actualAmount != null
                ? formatMoney(cost.actualAmount, cost.currencyCode)
                : "—"}
            </dd>
          </div>
          <div>
            <dt>Ngày hiệu lực</dt>
            <dd>{cost.effectiveDate}</dd>
          </div>
        </dl>

        {cost.recordStatus === "active" ? (
          <p className="cta-row">
            {canConfirm(cost.financialMaturity, cost.recordStatus) ? (
              <MaturityTransitionButton
                terms={terms}
                kind="cost"
                action="confirm"
                lineId={cost.id}
                currentAmount={cost.amount}
                currencyCode={cost.currencyCode}
              />
            ) : null}
            {canActualize(cost.financialMaturity, cost.recordStatus) ? (
              <MaturityTransitionButton
                terms={terms}
                kind="cost"
                action="actualize"
                lineId={cost.id}
                currentAmount={cost.amount}
                currencyCode={cost.currencyCode}
              />
            ) : null}
            <AdjustCostRevenueButton
              terms={terms}
              kind="cost"
              lineId={cost.id}
              currentAmount={cost.amount}
              currencyCode={cost.currencyCode}
              financialMaturity={cost.financialMaturity}
              buttonClassName="btn btn-sm"
            />
          </p>
        ) : null}

        <h2>{adjLabel} — lịch sử</h2>
        <AdjustmentHistoryTable
          terms={terms}
          adjustments={cost.adjustments ?? []}
          currencyCode={cost.currencyCode}
        />

        <h2>{allocLabel}</h2>
        {!billsRes.ok ? (
          <div className="alert alert-error" role="alert">
            {billsRes.message}
          </div>
        ) : null}

        {draft ? (
          <div className="cta-row" style={{ marginBottom: "1rem" }}>
            <FinalizeCostAllocationButton
              terms={terms}
              allocationId={draft.id}
              amount={draft.allocatableAmount || cost.amount}
              currencyCode={cost.currencyCode}
              billCount={draft.details?.length ?? 0}
            />
            <span className="muted">
              Phiên nháp v{draft.versionNo} ·{" "}
              {allocationBasisLabel(draft.allocationBasis)}
            </span>
          </div>
        ) : null}

        {allocations.length === 0 ? (
          <div className="empty-state" role="status">
            Chưa có phiên {allocLabel.toLowerCase()}. Tạo nháp bên dưới (≥2{" "}
            {billLabel}).
          </div>
        ) : (
          <div className="table-wrap">
            <table className="data-table">
              <caption className="sr-only">Lịch sử phiên phân bổ</caption>
              <thead>
                <tr>
                  <th scope="col">Phiên</th>
                  <th scope="col">Cơ sở</th>
                  <th scope="col">Trạng thái</th>
                  <th scope="col" className="num">
                    Phân bổ
                  </th>
                  <th scope="col">Chi tiết {billLabel}</th>
                  <th scope="col">Chốt lúc</th>
                </tr>
              </thead>
              <tbody>
                {allocations.map((a) => (
                  <tr key={a.id}>
                    <td>v{a.versionNo}</td>
                    <td>{allocationBasisLabel(a.allocationBasis)}</td>
                    <td>{allocationStatusLabel(a.allocationStatus)}</td>
                    <td className="num">
                      {formatMoney(a.allocatedAmount, cost.currencyCode)}
                      {a.allocatableAmount !== a.allocatedAmount &&
                      a.allocationStatus?.toLowerCase() === "draft"
                        ? ` / ${formatMoney(a.allocatableAmount, cost.currencyCode)}`
                        : ""}
                    </td>
                    <td>
                      <ul className="inline-list">
                        {(a.details ?? []).map((d) => (
                          <li key={d.id}>
                            <Link
                              className="row-link"
                              href={`/bills/${d.billId}`}
                            >
                              {billNoById.get(d.billId) ??
                                d.billId.slice(0, 8)}
                            </Link>
                            {a.allocationStatus?.toLowerCase() !== "draft"
                              ? `: ${formatMoney(d.allocatedAmount, cost.currencyCode)}`
                              : d.basisValue
                                ? ` (cơ sở ${d.basisValue})`
                                : ""}
                          </li>
                        ))}
                      </ul>
                    </td>
                    <td>
                      {a.finalizedAt ? formatDateTimeVi(a.finalizedAt) : "—"}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        <h2>Tạo phiên nháp mới</h2>
        <AllocateSharedCostForm
          terms={terms}
          costId={cost.id}
          allocatableAmount={cost.amount}
          currencyCode={cost.currencyCode}
          bills={bills.map((b) => ({ id: b.id, billNo: b.billNo }))}
          hasDraft={hasDraft}
        />
      </section>
    </AppShell>
  );
}
