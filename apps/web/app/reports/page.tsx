import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import {
  FinColors,
  GroupedBarChart,
  HorizontalBarChart,
} from "@/components/charts/FinanceCharts";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import { agingBucketLabel, getAgingSummary } from "@/lib/ap-ar";
import { getDashboardSummary } from "@/lib/control-desk";
import { formatMoney } from "@/lib/money";

export default async function ReportsPage() {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const terms = await fetchTerminology();
  const costLabel = term(terms, "COST", "Chi phí");
  const revenueLabel = term(terms, "REVENUE", "Doanh thu");
  const profitLabel = term(terms, "PROFIT", "Lợi nhuận");
  const billLabel = term(terms, "BILL", "Bill");
  const apLabel = term(terms, "ACCOUNTS_PAYABLE", "Công nợ phải trả");
  const arLabel = term(terms, "ACCOUNTS_RECEIVABLE", "Công nợ phải thu");
  const bestAvailableLabel = term(terms, "BEST_AVAILABLE", "Giá trị tốt nhất hiện có");

  const [summary, aging] = await Promise.all([
    getDashboardSummary(),
    getAgingSummary(),
  ]);

  const roll = summary.ok ? summary.data.baseCurrencyRollUp : null;
  const row0 = summary.ok ? summary.data.totalsByCurrency[0] : null;
  const currency = roll?.baseCurrency ?? row0?.currencyCode ?? "VND";
  const cost = roll?.costBestAvailableBase ?? row0?.costBestAvailable ?? 0;
  const revenue =
    roll?.revenueBestAvailableBase ?? row0?.revenueBestAvailable ?? 0;
  const profit =
    roll?.profitBestAvailableBase ?? row0?.profitBestAvailable ?? 0;

  const plBars = [
    { key: "cost", label: costLabel, value: cost, color: FinColors.cost },
    {
      key: "revenue",
      label: revenueLabel,
      value: revenue,
      color: FinColors.revenue,
    },
    {
      key: "profit",
      label: profitLabel,
      value: profit,
      color: FinColors.profit,
    },
  ];

  const agingBars =
    aging.ok && aging.data.payable
      ? aging.data.payable.buckets.map((b) => ({
          key: b.bucket,
          label: agingBucketLabel(b.bucket),
          value: b.outstanding,
          color: FinColors.agingMid,
        }))
      : [];

  return (
    <AppShell terms={terms} active="reports">
      <section className="panel panel-wide">
        <p className="breadcrumb">
          <Link href="/dashboard">Trang chủ</Link>
          {" / "}
          Báo cáo &amp; Phân tích
        </p>
        <h1>Báo cáo &amp; Phân tích</h1>
        <p className="lede">
          Read model quản trị — không phải sổ giao dịch. Mọi số liệu có thể truy ngược về{" "}
          {billLabel} và chứng từ nguồn.
        </p>

        {!summary.ok ? (
          <div className="alert alert-error" role="alert">
            {summary.message}
          </div>
        ) : (
          <>
            <div className="stat-grid" style={{ marginTop: "1rem" }}>
              <Link href="/costs" className="stat-card" style={{ textDecoration: "none" }}>
                <span className="stat-label">
                  {costLabel} ({bestAvailableLabel})
                </span>
                <strong className="stat-value">
                  {formatMoney(cost, currency)}
                </strong>
              </Link>
              <Link href="/revenues" className="stat-card" style={{ textDecoration: "none" }}>
                <span className="stat-label">
                  {revenueLabel} ({bestAvailableLabel})
                </span>
                <strong className="stat-value">
                  {formatMoney(revenue, currency)}
                </strong>
              </Link>
              <Link href="/bills" className="stat-card" style={{ textDecoration: "none" }}>
                <span className="stat-label">{profitLabel}</span>
                <strong
                  className="stat-value"
                  style={{
                    color: profit < 0 ? "var(--danger)" : undefined,
                  }}
                >
                  {formatMoney(profit, currency)}
                </strong>
              </Link>
              <Link href="/bills" className="stat-card" style={{ textDecoration: "none" }}>
                <span className="stat-label">Số {billLabel}</span>
                <strong className="stat-value">{summary.data.billCount}</strong>
              </Link>
            </div>

            <div className="dash-layout" style={{ marginTop: "1.25rem" }}>
              <div className="panel">
                <GroupedBarChart
                  caption={`${costLabel} / ${revenueLabel} / ${profitLabel}`}
                  series={plBars}
                />
              </div>
              <div className="panel">
                {!aging.ok ? (
                  <div className="alert alert-error" role="alert">
                    {aging.message}
                  </div>
                ) : (
                  <HorizontalBarChart
                    caption="Tuổi nợ AP (projection)"
                    series={agingBars}
                  />
                )}
                <p className="cta-row">
                  <Link className="btn btn-ghost btn-sm" href="/ap-ar?tab=ap">
                    {apLabel}
                  </Link>
                  <Link className="btn btn-ghost btn-sm" href="/ap-ar?tab=ar">
                    {arLabel}
                  </Link>
                  <Link className="btn btn-ghost btn-sm" href="/ap-ar/aging">
                    Chi tiết aging
                  </Link>
                </p>
              </div>
            </div>
          </>
        )}

        <div className="hub-links">
          <Link href="/dashboard" className="panel">
            <h2 className="section-title">Trang chủ điều hành</h2>
            <p className="muted">KPI + việc cần xử lý + phân tách độ chín.</p>
            <span className="btn btn-ghost btn-sm">Mở →</span>
          </Link>
          <Link href="/bills" className="panel">
            <h2 className="section-title">Lợi nhuận theo {billLabel}</h2>
            <p className="muted">Drill-down hồ sơ tài chính từng Bill (Financial Anchor).</p>
            <span className="btn btn-ghost btn-sm">Mở →</span>
          </Link>
          <Link href="/financial-closes" className="panel">
            <h2 className="section-title">Snapshot chốt kỳ</h2>
            <p className="muted">Báo cáo sau chốt lấy từ snapshot bất biến.</p>
            <span className="btn btn-ghost btn-sm">Mở →</span>
          </Link>
        </div>
      </section>
    </AppShell>
  );
}
