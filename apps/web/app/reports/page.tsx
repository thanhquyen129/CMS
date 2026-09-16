import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import {
  FinColors,
  GroupedBarChart,
  HorizontalBarChart,
} from "@/components/charts/FinanceCharts";
import { ListPageHeader } from "@/components/list/ListPageHeader";
import { StatCardGrid } from "@/components/list/StatCardGrid";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import { agingBucketLabel, getAgingSummary } from "@/lib/ap-ar";
import { getDashboardSummary } from "@/lib/control-desk";
import { formatDateTimeVi, formatMoney } from "@/lib/money";

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

  const links = [
    {
      href: "/dashboard",
      title: "Trang chủ điều hành",
      desc: "KPI + việc cần xử lý + phân tách độ chín.",
    },
    {
      href: "/bills",
      title: `Lợi nhuận theo ${billLabel}`,
      desc: "Drill-down hồ sơ tài chính từng Bill (Financial Anchor).",
    },
    {
      href: "/financial-closes",
      title: "Snapshot chốt kỳ",
      desc: "Báo cáo sau chốt lấy từ snapshot bất biến.",
    },
    {
      href: "/costs",
      title: `Chi tiết ${costLabel.toLowerCase()}`,
      desc: "Danh sách + phân tách độ chín theo dòng.",
    },
    {
      href: "/revenues",
      title: `Chi tiết ${revenueLabel.toLowerCase()}`,
      desc: "Danh sách + phân tách độ chín theo dòng.",
    },
    {
      href: "/ap-ar",
      title: `${apLabel} / ${arLabel}`,
      desc: "Số dư còn lại, tuổi nợ và lịch sử tất toán.",
    },
    {
      href: "/settlements",
      title: "Thanh toán & Thu tiền",
      desc: "Tổng phân bổ, chưa áp dụng theo giao dịch tiền mặt.",
    },
  ];

  return (
    <AppShell terms={terms} active="reports">
      <section className="panel panel-wide">
        <ListPageHeader
          breadcrumbs={[
            { href: "/dashboard", label: "Trang chủ" },
            { label: "Báo cáo & Phân tích" },
          ]}
          title="Báo cáo & Phân tích"
          lede={
            <>
              Read model quản trị — không phải sổ giao dịch. Mọi số liệu có thể truy ngược về{" "}
              {billLabel} và chứng từ nguồn.
            </>
          }
        />
        {summary.ok ? (
          <p className="meta-line muted">
            Tại thời điểm: {formatDateTimeVi(summary.data.asOfTimestamp)} — số liệu
            projection, không phải sổ ghi tài chính.
          </p>
        ) : null}

        {!summary.ok ? (
          <div className="alert alert-error" role="alert">
            {summary.message}
          </div>
        ) : (
          <>
            <StatCardGrid
              cards={[
                {
                  key: "cost",
                  label: `${costLabel} (${bestAvailableLabel})`,
                  value: formatMoney(cost, currency),
                  tone: "warning",
                  href: "/costs",
                },
                {
                  key: "revenue",
                  label: `${revenueLabel} (${bestAvailableLabel})`,
                  value: formatMoney(revenue, currency),
                  tone: "success",
                  href: "/revenues",
                },
                {
                  key: "profit",
                  label: profitLabel,
                  value: formatMoney(profit, currency),
                  tone: profit < 0 ? "danger" : "primary",
                  href: "/bills",
                },
                {
                  key: "bills",
                  label: `Số ${billLabel}`,
                  value: summary.data.billCount,
                  href: "/bills",
                },
              ]}
            />

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

        <div className="hub-module-tabs" role="navigation" aria-label="Liên kết báo cáo">
          {links.map((item) => (
            <Link key={item.href} href={item.href} className="hub-module-tab">
              <strong>{item.title}</strong>
              <span>{item.desc}</span>
            </Link>
          ))}
        </div>
      </section>
    </AppShell>
  );
}
