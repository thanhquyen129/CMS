import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { AnalyticsRow, AnalyticsPanel } from "@/components/list/AnalyticsRow";
import { ListPageHeader } from "@/components/list/ListPageHeader";
import { StatCardGrid } from "@/components/list/StatCardGrid";
import { FinColors, HorizontalBarChart } from "@/components/charts/FinanceCharts";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import { getDashboardSummary } from "@/lib/control-desk";
import { formatMoney } from "@/lib/money";

export default async function ControlHubPage() {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const terms = await fetchTerminology();
  const exceptionQueueLabel = term(terms, "EXCEPTION_QUEUE", "Hàng đợi ngoại lệ");
  const varianceLabel = term(terms, "VARIANCE", "Chênh lệch");
  const approvalQueueLabel = term(terms, "APPROVAL_QUEUE", "Hàng đợi phê duyệt");
  const reconQueueLabel = term(terms, "RECONCILIATION_QUEUE", "Hàng đợi đối soát");
  const bankFeedLabel = term(terms, "BANK_FEED", "Sao kê ngân hàng");
  const summary = await getDashboardSummary();

  const links = [
    {
      href: "/queues/exceptions",
      title: exceptionQueueLabel,
      desc: "Ngoại lệ cần xử lý — khác với chênh lệch.",
      count: summary.ok ? summary.data.openExceptionCount : null,
    },
    {
      href: "/queues/variances",
      title: `Hàng đợi ${varianceLabel.toLowerCase()}`,
      desc: "Chênh lệch số liệu cần giải trình — không gộp với ngoại lệ.",
      count: summary.ok ? summary.data.openVarianceCount : null,
    },
    {
      href: "/queues/approvals",
      title: approvalQueueLabel,
      desc: "Phê duyệt độc lập với phân quyền (Permission ≠ Approval).",
      count: summary.ok ? summary.data.pendingApprovalCount : null,
    },
    {
      href: "/queues/reconciliations",
      title: reconQueueLabel,
      desc: "Đối soát chờ hoàn tất.",
      count: summary.ok ? summary.data.openReconciliationCount : null,
    },
    {
      href: "/reconciliations",
      title: "Đối soát & Matching",
      desc: "Không gian làm việc đối soát chứng từ / sao kê.",
      count: null,
    },
    {
      href: "/bank-feed",
      title: bankFeedLabel,
      desc: "Dòng sao kê chưa khớp — chưa đồng nghĩa đã tất toán.",
      count: summary.ok ? summary.data.unmatchedBankFeedCount : null,
    },
  ];

  const roll = summary.ok ? summary.data.baseCurrencyRollUp : null;
  const row0 = summary.ok ? summary.data.totalsByCurrency[0] : null;

  return (
    <AppShell terms={terms} active="control">
      <section className="panel panel-wide">
        <ListPageHeader
          breadcrumbs={[
            { href: "/dashboard", label: "Trang chủ" },
            { label: "Kiểm soát tài chính" },
          ]}
          title="Kiểm soát tài chính"
          lede="Workbench đối soát, chênh lệch, ngoại lệ và phê duyệt — giữ minh bạch dữ liệu trước khi chốt kỳ."
        />

        {!summary.ok ? (
          <div className="alert alert-error" role="alert">
            {summary.message}
          </div>
        ) : (
          <>
            <StatCardGrid
              cards={[
                {
                  key: "approval",
                  label: approvalQueueLabel,
                  value: summary.data.pendingApprovalCount,
                  tone: "primary",
                  href: "/queues/approvals",
                },
                {
                  key: "exception",
                  label: exceptionQueueLabel,
                  value: summary.data.openExceptionCount,
                  tone: "warning",
                  href: "/queues/exceptions",
                },
                {
                  key: "overdue",
                  label: "Ngoại lệ quá hạn",
                  value: summary.data.overdueExceptionCount,
                  tone: summary.data.overdueExceptionCount > 0 ? "danger" : "default",
                  href: "/queues/exceptions?overdueOnly=1",
                },
                {
                  key: "variance",
                  label: `${varianceLabel} đang mở`,
                  value: summary.data.openVarianceCount,
                  tone: "info",
                  href: "/queues/variances",
                },
                {
                  key: "recon",
                  label: reconQueueLabel,
                  value: summary.data.openReconciliationCount,
                  href: "/queues/reconciliations",
                },
                {
                  key: "bank",
                  label: `${bankFeedLabel} chưa khớp`,
                  value: summary.data.unmatchedBankFeedCount,
                  tone: summary.data.unmatchedBankFeedCount > 0 ? "warning" : "default",
                  href: "/bank-feed?status=unmatched",
                },
              ]}
            />

            <AnalyticsRow columns={1}>
              <AnalyticsPanel>
                <HorizontalBarChart
                  caption="Cơ cấu hàng đợi kiểm soát"
                  series={[
                    {
                      key: "appr",
                      label: approvalQueueLabel,
                      value: summary.data.pendingApprovalCount,
                      color: FinColors.workMuted,
                    },
                    {
                      key: "ex",
                      label: exceptionQueueLabel,
                      value: summary.data.openExceptionCount,
                      color: FinColors.work,
                    },
                    {
                      key: "od",
                      label: "Quá hạn",
                      value: summary.data.overdueExceptionCount,
                      color: FinColors.workDanger,
                    },
                    {
                      key: "var",
                      label: varianceLabel,
                      value: summary.data.openVarianceCount,
                      color: FinColors.workWarn,
                    },
                    {
                      key: "recon",
                      label: reconQueueLabel,
                      value: summary.data.openReconciliationCount,
                      color: FinColors.work,
                    },
                    {
                      key: "bank",
                      label: bankFeedLabel,
                      value: summary.data.unmatchedBankFeedCount,
                      color: FinColors.workWarn,
                    },
                  ]}
                />
              </AnalyticsPanel>
            </AnalyticsRow>
          </>
        )}

        <div className="hub-module-tabs" role="navigation" aria-label="Hàng đợi kiểm soát">
          {links.map((item) => (
            <Link key={item.href} href={item.href} className="hub-module-tab">
              <strong>
                {item.title}
                {item.count != null ? ` (${item.count})` : ""}
              </strong>
              <span>{item.desc}</span>
            </Link>
          ))}
        </div>

        {summary.ok && (roll || row0) ? (
          <p className="muted" style={{ marginTop: "1.25rem" }}>
            Giá trị tốt nhất (projection)
            {roll
              ? `: chi phí ${formatMoney(roll.costBestAvailableBase ?? 0, roll.baseCurrency)} · doanh thu ${formatMoney(roll.revenueBestAvailableBase ?? 0, roll.baseCurrency)}`
              : row0
                ? `: chi phí ${formatMoney(row0.costBestAvailable ?? 0, row0.currencyCode)} · doanh thu ${formatMoney(row0.revenueBestAvailable ?? 0, row0.currencyCode)}`
                : null}
            . Không phải sổ ghi tài chính.
          </p>
        ) : null}
      </section>
    </AppShell>
  );
}
