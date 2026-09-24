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

type SearchParams = Promise<{ view?: string; asOf?: string }>;

const VIEW_OPTIONS = [
  { value: "expected", label: "Dự kiến" },
  { value: "confirmed", label: "Đã xác nhận" },
  { value: "actual", label: "Thực tế" },
  { value: "best", label: "Giá trị tốt nhất" },
] as const;

export default async function ReportsPage({
  searchParams,
}: {
  searchParams: SearchParams;
}) {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const sp = await searchParams;
  const view = VIEW_OPTIONS.some((v) => v.value === sp.view)
    ? (sp.view as string)
    : "best";
  const terms = await fetchTerminology();
  const costLabel = term(terms, "COST", "Chi phí");
  const revenueLabel = term(terms, "REVENUE", "Doanh thu");
  const profitLabel = term(terms, "PROFIT", "Lợi nhuận");
  const billLabel = term(terms, "BILL", "Bill");
  const apLabel = term(terms, "ACCOUNTS_PAYABLE", "Công nợ phải trả");
  const arLabel = term(terms, "ACCOUNTS_RECEIVABLE", "Công nợ phải thu");
  const viewLabel =
    VIEW_OPTIONS.find((v) => v.value === view)?.label ?? "Giá trị tốt nhất";

  const [summary, aging] = await Promise.all([
    getDashboardSummary(),
    getAgingSummary(sp.asOf ? { asOf: sp.asOf } : undefined),
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
      href: `/revenues/report?view=${view}`,
      title: `Lãi gộp / ${revenueLabel}`,
      desc: `Maturity tường minh: ${viewLabel}. Drill theo nhóm khách, dịch vụ, tuyến.`,
    },
    {
      href: `/reports/bills?view=${encodeURIComponent(view)}${sp.asOf ? `&asOf=${encodeURIComponent(sp.asOf)}` : ""}`,
      title: `Lợi nhuận theo ${billLabel}`,
      desc: "Báo cáo theo maturity và tiền tệ báo cáo. Mở hồ sơ Bill để đối chiếu.",
    },
    {
      href: "/costs",
      title: `Phân tích ${costLabel.toLowerCase()}`,
      desc: "Danh sách và phân bổ theo độ chín.",
    },
    {
      href: sp.asOf ? `/ap-ar/aging?asOf=${sp.asOf}` : "/ap-ar/aging",
      title: `Tuổi nợ ${apLabel} / ${arLabel}`,
      desc: "As-of loại trừ thanh toán/thu phát sinh sau mốc.",
    },
    {
      href: sp.asOf ? `/reports/cash?asOf=${sp.asOf}` : "/reports/cash",
      title: "Tiền và tất toán",
      desc: "Thanh toán/thu chưa gán; drill về giao dịch.",
    },
    {
      href: "/queues/exceptions",
      title: "Ngoại lệ",
      desc: "Hàng đợi ngoại lệ đang mở / chờ miễn.",
    },
    {
      href: "/financial-closes",
      title: "Báo cáo chốt",
      desc: "P&L và chỉ số từ snapshot bất biến.",
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
              Số liệu lấy từ API dashboard và aging — không minh họa giả. Chọn maturity
              và as-of tường minh; không trộn lớp độ chín. Drill về {billLabel} và chứng
              từ nguồn.
            </>
          }
        />

        <form className="filter-bar" method="get">
          <label>
            Maturity
            <select name="view" defaultValue={view}>
              {VIEW_OPTIONS.map((o) => (
                <option key={o.value} value={o.value}>
                  {o.label}
                </option>
              ))}
            </select>
          </label>
          <label>
            As-of
            <input type="date" name="asOf" defaultValue={sp.asOf ?? ""} />
          </label>
          <button type="submit" className="btn btn-sm">
            Áp dụng
          </button>
        </form>

        {summary.ok ? (
          <p className="meta-line muted">
            Tổng quan dashboard tại {formatDateTimeVi(summary.data.asOfTimestamp)}{" "}
            — KPI dưới đây vẫn là {viewLabel} trên projection; báo cáo chi tiết
            dùng bộ lọc phía trên.
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
                  label: `${costLabel} (${viewLabel})`,
                  value: formatMoney(cost, currency),
                  tone: "warning",
                  href: "/costs",
                },
                {
                  key: "revenue",
                  label: `${revenueLabel} (${viewLabel})`,
                  value: formatMoney(revenue, currency),
                  tone: "success",
                  href: `/revenues/report?view=${view}`,
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
                {summary.data.monthlySeriesNote ? (
                  <p className="muted small">{summary.data.monthlySeriesNote}</p>
                ) : null}
                {(summary.data.monthlySeries ?? []).some(
                  (p) => p.costBestAvailable != null || p.revenueBestAvailable != null
                ) ? (
                  <table className="data-table">
                    <caption className="sr-only">Chuỗi tháng theo ngày hiệu lực</caption>
                    <thead>
                      <tr>
                        <th scope="col">Tháng</th>
                        <th scope="col" className="num">{costLabel}</th>
                        <th scope="col" className="num">{revenueLabel}</th>
                      </tr>
                    </thead>
                    <tbody>
                      {(summary.data.monthlySeries ?? [])
                        .filter(
                          (p) =>
                            p.costBestAvailable != null || p.revenueBestAvailable != null
                        )
                        .map((p) => (
                          <tr key={p.month}>
                            <th scope="row">Tháng {p.month}</th>
                            <td className="num">
                              {p.costBestAvailable != null
                                ? formatMoney(p.costBestAvailable, row0?.currencyCode ?? currency)
                                : "—"}
                            </td>
                            <td className="num">
                              {p.revenueBestAvailable != null
                                ? formatMoney(p.revenueBestAvailable, row0?.currencyCode ?? currency)
                                : "—"}
                            </td>
                          </tr>
                        ))}
                    </tbody>
                  </table>
                ) : null}
              </div>
              <div className="panel">
                {!aging.ok ? (
                  <div className="alert alert-error" role="alert">
                    {aging.message}
                  </div>
                ) : (
                  <HorizontalBarChart
                    caption={`Tuổi nợ AP${sp.asOf ? ` · as-of ${sp.asOf}` : ""}`}
                    series={agingBars}
                  />
                )}
                <p className="cta-row">
                  <Link
                    className="btn btn-ghost btn-sm"
                    href={sp.asOf ? `/ap-ar/aging?asOf=${sp.asOf}` : "/ap-ar/aging"}
                  >
                    Chi tiết aging
                  </Link>
                  <Link className="btn btn-ghost btn-sm" href="/reports/cash">
                    Tiền &amp; tất toán
                  </Link>
                </p>
              </div>
            </div>
          </>
        )}

        <div className="hub-module-tabs" role="navigation" aria-label="Danh mục báo cáo">
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
