import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
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
        <p className="breadcrumb">
          <Link href="/dashboard">Trang chủ</Link>
          {" / "}
          Kiểm soát tài chính
        </p>
        <h1>Kiểm soát tài chính</h1>
        <p className="lede">
          Workbench đối soát, chênh lệch, ngoại lệ và phê duyệt — giữ minh bạch dữ liệu
          trước khi chốt kỳ.
        </p>

        {!summary.ok ? (
          <div className="alert alert-error" role="alert">
            {summary.message}
          </div>
        ) : (
          <div className="stat-grid" style={{ marginTop: "1rem" }}>
            <div className="stat-card">
              <span className="stat-label">{approvalQueueLabel}</span>
              <strong className="stat-value">{summary.data.pendingApprovalCount}</strong>
            </div>
            <div className="stat-card">
              <span className="stat-label">{exceptionQueueLabel}</span>
              <strong className="stat-value">{summary.data.openExceptionCount}</strong>
            </div>
            <div className="stat-card">
              <span className="stat-label">{varianceLabel} đang mở</span>
              <strong className="stat-value">{summary.data.openVarianceCount}</strong>
            </div>
            <div className="stat-card">
              <span className="stat-label">{reconQueueLabel}</span>
              <strong className="stat-value">{summary.data.openReconciliationCount}</strong>
            </div>
            <div className="stat-card">
              <span className="stat-label">{bankFeedLabel} chưa khớp</span>
              <strong className="stat-value">{summary.data.unmatchedBankFeedCount}</strong>
            </div>
          </div>
        )}

        <div className="hub-links">
          {links.map((item) => (
            <Link key={item.href} href={item.href} className="panel">
              <h2 className="section-title">
                {item.title}
                {item.count != null ? (
                  <span className="muted" style={{ fontWeight: 500, marginLeft: "0.4rem" }}>
                    ({item.count})
                  </span>
                ) : null}
              </h2>
              <p className="muted">{item.desc}</p>
              <span className="btn btn-ghost btn-sm">Mở →</span>
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
