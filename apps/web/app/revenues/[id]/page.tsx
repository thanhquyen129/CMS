import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { AdjustCostRevenueButton } from "@/components/AdjustCostRevenueButton";
import {
  AdjustmentHistoryTable,
  LineDetailBackLink,
} from "@/components/AdjustmentHistoryTable";
import { MapRevenueForm } from "@/components/MapRevenueForm";
import { MaturityTransitionButton } from "@/components/MaturityTransitionButton";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import { listBills } from "@/lib/bills";
import { getRevenue } from "@/lib/costs-revenues-server";
import {
  canActualize,
  canConfirm,
  maturityLabelKey,
} from "@/lib/costs-revenues";
import { formatMoney } from "@/lib/money";

type Params = Promise<{ id: string }>;

export default async function RevenueDetailPage({
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
  const revenueLabel = term(terms, "REVENUE", "Doanh thu");
  const billLabel = term(terms, "BILL", "Bill");
  const expected = term(terms, "EXPECTED", "Dự kiến");
  const confirmed = term(terms, "CONFIRMED", "Đã xác nhận");
  const actual = term(terms, "ACTUAL", "Thực tế");
  const adjLabel = term(terms, "ADJUSTMENT", "Điều chỉnh");

  const revRes = await getRevenue(id);
  if (!revRes.ok) {
    return (
      <AppShell terms={terms} active="revenues">
        <section className="panel">
          <LineDetailBackLink href="/bills" label={`Danh sách ${billLabel}`} />
          <div className="alert alert-error" role="alert">
            {revRes.message}
          </div>
        </section>
      </AppShell>
    );
  }

  const revenue = revRes.data;
  const maturityKey = maturityLabelKey(revenue.financialMaturity);
  const maturityVi =
    maturityKey === "EXPECTED"
      ? expected
      : maturityKey === "CONFIRMED"
        ? confirmed
        : maturityKey === "ACTUAL"
          ? actual
          : revenue.financialMaturity;

  const backHref = `/bills/${revenue.billId}`;
  const adjustments = revenue.adjustments ?? [];
  const billList = await listBills(undefined, { page: 1, pageSize: 50 });
  const billOptions = billList.ok
    ? billList.data.items.map((b) => ({ id: b.id, billNo: b.billNo }))
    : [];

  return (
    <AppShell terms={terms} active="revenues">
      <section className="panel panel-wide">
        <LineDetailBackLink href={backHref} label={`Hồ sơ ${billLabel}`} />
        <h1>
          {revenue.revenueTypeCode ||
            `${revenueLabel} ${revenue.id.slice(0, 8)}`}
        </h1>
        <p className="lede">
          {maturityVi} ·{" "}
          <strong>{formatMoney(revenue.amount, revenue.currencyCode)}</strong>
          {revenue.recordStatus !== "active"
            ? ` · ${revenue.recordStatus}`
            : ""}
        </p>

        <dl className="metric-grid">
          <div>
            <dt>{expected}</dt>
            <dd>{formatMoney(revenue.expectedAmount, revenue.currencyCode)}</dd>
          </div>
          <div>
            <dt>{confirmed}</dt>
            <dd>
              {revenue.confirmedAmount != null
                ? formatMoney(revenue.confirmedAmount, revenue.currencyCode)
                : "—"}
            </dd>
          </div>
          <div>
            <dt>{actual}</dt>
            <dd>
              {revenue.actualAmount != null
                ? formatMoney(revenue.actualAmount, revenue.currencyCode)
                : "—"}
            </dd>
          </div>
          <div>
            <dt>Ngày hiệu lực</dt>
            <dd>{revenue.effectiveDate}</dd>
          </div>
        </dl>

        {revenue.recordStatus === "active" ? (
          <p className="cta-row">
            {canConfirm(revenue.financialMaturity, revenue.recordStatus) ? (
              <MaturityTransitionButton
                terms={terms}
                kind="revenue"
                action="confirm"
                lineId={revenue.id}
                currentAmount={revenue.amount}
                currencyCode={revenue.currencyCode}
                rowVersion={revenue.rowVersion}
              />
            ) : null}
            {canActualize(revenue.financialMaturity, revenue.recordStatus) ? (
              <MaturityTransitionButton
                terms={terms}
                kind="revenue"
                action="actualize"
                lineId={revenue.id}
                currentAmount={revenue.amount}
                currencyCode={revenue.currencyCode}
                rowVersion={revenue.rowVersion}
              />
            ) : null}
            <AdjustCostRevenueButton
              terms={terms}
              kind="revenue"
              lineId={revenue.id}
              currentAmount={revenue.amount}
              currencyCode={revenue.currencyCode}
              financialMaturity={revenue.financialMaturity}
              rowVersion={revenue.rowVersion}
              buttonClassName="btn btn-sm"
            />
            <Link className="btn btn-ghost btn-sm" href={backHref}>
              Về {billLabel}
            </Link>
          </p>
        ) : null}

        <MapRevenueForm
          revenueId={revenue.id}
          amount={revenue.amount}
          currencyCode={revenue.currencyCode}
          bills={billOptions}
        />

        <h2>{adjLabel} — lịch sử</h2>
        <p className="note">
          Mỗi lần đổi số lớp hiện tại tạo một dòng lịch sử (C-009). Không silent
          overwrite.
        </p>
        <AdjustmentHistoryTable
          terms={terms}
          adjustments={adjustments}
          currencyCode={revenue.currencyCode}
        />
      </section>
    </AppShell>
  );
}
