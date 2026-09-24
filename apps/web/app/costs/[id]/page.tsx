import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { AdjustCostRevenueButton } from "@/components/AdjustCostRevenueButton";
import {
  AdjustmentHistoryTable,
  LineDetailBackLink,
} from "@/components/AdjustmentHistoryTable";
import { AuditTrailPanel } from "@/components/AuditTrailPanel";
import { MaturityTransitionButton } from "@/components/MaturityTransitionButton";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import { getCost } from "@/lib/costs-revenues-server";
import {
  canActualize,
  canConfirm,
  isSharedCost,
  maturityLabelKey,
} from "@/lib/costs-revenues";
import { formatMoney } from "@/lib/money";

type Params = Promise<{ id: string }>;

export default async function CostDetailPage({ params }: { params: Params }) {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const { id } = await params;
  const terms = await fetchTerminology();
  const costLabel = term(terms, "COST", "Chi phí");
  const billLabel = term(terms, "BILL", "Bill");
  const expected = term(terms, "EXPECTED", "Dự kiến");
  const confirmed = term(terms, "CONFIRMED", "Đã xác nhận");
  const actual = term(terms, "ACTUAL", "Thực tế");
  const adjLabel = term(terms, "ADJUSTMENT", "Điều chỉnh");

  const costRes = await getCost(id);
  if (!costRes.ok) {
    return (
      <AppShell terms={terms} active="costs">
        <section className="panel">
          <LineDetailBackLink href="/bills" label={`Danh sách ${billLabel}`} />
          <div className="alert alert-error" role="alert">
            {costRes.message}
          </div>
        </section>
      </AppShell>
    );
  }

  const cost = costRes.data;
  if (isSharedCost(cost.attributionType)) {
    redirect(`/costs/shared/${cost.id}`);
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

  const backHref = cost.billId ? `/bills/${cost.billId}` : "/bills";
  const adjustments = cost.adjustments ?? [];

  return (
    <AppShell terms={terms} active="costs">
      <section className="panel panel-wide">
        <LineDetailBackLink
          href={backHref}
          label={cost.billId ? `Hồ sơ ${billLabel}` : `Danh sách ${billLabel}`}
        />
        <h1>
          {cost.costTypeCode || `${costLabel} ${cost.id.slice(0, 8)}`}
        </h1>
        <p className="lede">
          Trực tiếp · {maturityVi} ·{" "}
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
                rowVersion={cost.rowVersion}
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
                rowVersion={cost.rowVersion}
              />
            ) : null}
            <AdjustCostRevenueButton
              terms={terms}
              kind="cost"
              lineId={cost.id}
              currentAmount={cost.amount}
              currencyCode={cost.currencyCode}
              financialMaturity={cost.financialMaturity}
              rowVersion={cost.rowVersion}
              buttonClassName="btn btn-sm"
            />
            {cost.billId ? (
              <Link className="btn btn-ghost btn-sm" href={backHref}>
                Về {billLabel}
              </Link>
            ) : null}
          </p>
        ) : null}

        <h2>{adjLabel} — lịch sử</h2>
        <p className="note">
          Mỗi lần đổi số lớp hiện tại tạo một dòng lịch sử (C-009). Không silent
          overwrite.
        </p>
        <AdjustmentHistoryTable
          terms={terms}
          adjustments={adjustments}
          currencyCode={cost.currencyCode}
        />

        <AuditTrailPanel
          terms={terms}
          objectType="cost"
          objectId={cost.id}
        />
      </section>
    </AppShell>
  );
}
