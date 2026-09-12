import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import {
  getDashboardSummary,
} from "@/lib/control-desk";
import { formatDateTimeVi, formatMoney } from "@/lib/money";

export default async function DashboardPage() {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const terms = await fetchTerminology();
  const summaryLabel = term(terms, "DASHBOARD_SUMMARY", "Tóm tắt bảng điều khiển");
  const dashboardLabel = term(terms, "DASHBOARD", "Bảng điều khiển");
  const billLabel = term(terms, "BILL", "Bill");
  const exceptionQueueLabel = term(terms, "EXCEPTION_QUEUE", "Hàng đợi ngoại lệ");
  const approvalQueueLabel = term(terms, "APPROVAL_QUEUE", "Hàng đợi phê duyệt");
  const reconQueueLabel = term(terms, "RECONCILIATION_QUEUE", "Hàng đợi đối soát");
  const overdueLabel = term(terms, "OVERDUE_EXCEPTION_COUNT", "Số ngoại lệ quá hạn");
  const openVarianceLabel = term(terms, "OPEN_VARIANCE_COUNT", "Số chênh lệch đang mở");
  const bestAvailableLabel = term(terms, "BEST_AVAILABLE", "Giá trị tốt nhất hiện có");
  const costLabel = term(terms, "COST", "Chi phí");
  const revenueLabel = term(terms, "REVENUE", "Doanh thu");
  const profitLabel = term(terms, "PROFIT", "Lợi nhuận");
  const closeLabel = term(terms, "FINANCIAL_CLOSE", "Chốt tài chính");
  const asOfLabel = term(terms, "AS_OF", "Tại thời điểm");
  const rollUpLabel = term(terms, "BASE_CURRENCY_ROLLUP", "Cộng gộp theo tiền tệ cơ sở");
  const bankFeedLabel = term(terms, "BANK_FEED", "Sao kê ngân hàng");

  const result = await getDashboardSummary();

  return (
    <AppShell terms={terms} active="dashboard">
      <section className="panel panel-wide">
        <h1>{summaryLabel}</h1>
        <p className="lede">
          Việc cần xử lý trên {dashboardLabel}: ngoại lệ, phê duyệt, đối soát, và
          tổng {bestAvailableLabel} theo tiền tệ (projection — không phải sổ cái).
        </p>

        {!result.ok ? (
          <div className="alert alert-error" role="alert">
            {result.message}
          </div>
        ) : (
          <>
            <p className="meta-line muted">
              {asOfLabel}: {formatDateTimeVi(result.data.asOfTimestamp)}
            </p>

            <div className="stat-grid" role="list">
              <Link
                className="stat-card"
                href="/queues/exceptions"
                role="listitem"
              >
                <span className="stat-label">{exceptionQueueLabel}</span>
                <span className="stat-value">{result.data.openExceptionCount}</span>
                <span className="stat-hint">Mở hàng đợi ngoại lệ</span>
              </Link>
              <Link
                className="stat-card"
                href="/queues/approvals"
                role="listitem"
              >
                <span className="stat-label">{approvalQueueLabel}</span>
                <span className="stat-value">{result.data.pendingApprovalCount}</span>
                <span className="stat-hint">Mở hàng đợi phê duyệt</span>
              </Link>
              <Link
                className="stat-card"
                href="/queues/reconciliations"
                role="listitem"
              >
                <span className="stat-label">{reconQueueLabel}</span>
                <span className="stat-value">→</span>
                <span className="stat-hint">Phiên đang mở</span>
              </Link>
              <div className="stat-card stat-card-static" role="listitem">
                <span className="stat-label">{overdueLabel}</span>
                <span
                  className={
                    result.data.overdueExceptionCount > 0
                      ? "stat-value stat-warn"
                      : "stat-value"
                  }
                >
                  {result.data.overdueExceptionCount}
                </span>
                {result.data.overdueExceptionCount > 0 ? (
                  <Link className="stat-hint row-link" href="/queues/exceptions?overdueOnly=1">
                    Xem quá hạn
                  </Link>
                ) : (
                  <span className="stat-hint">Không có quá hạn</span>
                )}
              </div>
              <div className="stat-card stat-card-static" role="listitem">
                <span className="stat-label">Số {billLabel}</span>
                <span className="stat-value">{result.data.billCount}</span>
                <Link className="stat-hint row-link" href="/bills">
                  Mở danh sách {billLabel}
                </Link>
              </div>
              <Link
                className="stat-card"
                href="/queues/reconciliations"
                role="listitem"
              >
                <span className="stat-label">{openVarianceLabel}</span>
                <span className="stat-value">{result.data.openVarianceCount}</span>
                <span className="stat-hint">Xem phiên đối soát</span>
              </Link>
              <div className="stat-card stat-card-static" role="listitem">
                <span className="stat-label">{closeLabel} đang mở</span>
                <span className="stat-value">{result.data.openCloseCount}</span>
                <Link className="stat-hint row-link" href="/financial-closes">
                  Mở sổ chốt
                </Link>
              </div>
              <Link
                className="stat-card"
                href="/bank-feed?status=unmatched"
                role="listitem"
              >
                <span className="stat-label">{bankFeedLabel}</span>
                <span className="stat-value">→</span>
                <span className="stat-hint">Dòng chưa đối soát</span>
              </Link>
            </div>

            <h2 className="section-title">{bestAvailableLabel} theo tiền tệ</h2>
            {result.data.totalsByCurrency.length === 0 ? (
              <div className="empty-state" role="status">
                Chưa có {costLabel}/{revenueLabel} để tổng hợp. Tạo dòng trên {billLabel}{" "}
                rồi quay lại.
              </div>
            ) : (
              <div className="table-wrap">
                <table className="data-table">
                  <thead>
                    <tr>
                      <th scope="col">Tiền tệ</th>
                      <th scope="col" className="num">
                        {costLabel}
                      </th>
                      <th scope="col" className="num">
                        {revenueLabel}
                      </th>
                      <th scope="col" className="num">
                        {profitLabel}
                      </th>
                    </tr>
                  </thead>
                  <tbody>
                    {result.data.totalsByCurrency.map((row) => (
                      <tr key={row.currencyCode}>
                        <td>{row.currencyCode}</td>
                        <td className="num">
                          {formatMoney(row.costBestAvailable, row.currencyCode)}
                        </td>
                        <td className="num">
                          {formatMoney(row.revenueBestAvailable, row.currencyCode)}
                        </td>
                        <td
                          className={
                            row.profitBestAvailable < 0 ? "num neg" : "num"
                          }
                        >
                          {formatMoney(row.profitBestAvailable, row.currencyCode)}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}

            {result.data.baseCurrencyRollUp ? (
              <>
                <h2 className="section-title sm">{rollUpLabel}</h2>
                <dl className="metric-grid">
                  <div>
                    <dt>Tiền tệ cơ sở</dt>
                    <dd>{result.data.baseCurrencyRollUp.baseCurrency}</dd>
                  </div>
                  <div>
                    <dt>{costLabel}</dt>
                    <dd>
                      {formatMoney(
                        result.data.baseCurrencyRollUp.costBestAvailableBase,
                        result.data.baseCurrencyRollUp.baseCurrency
                      )}
                    </dd>
                  </div>
                  <div>
                    <dt>{revenueLabel}</dt>
                    <dd>
                      {formatMoney(
                        result.data.baseCurrencyRollUp.revenueBestAvailableBase,
                        result.data.baseCurrencyRollUp.baseCurrency
                      )}
                    </dd>
                  </div>
                  <div>
                    <dt>{profitLabel}</dt>
                    <dd
                      className={
                        result.data.baseCurrencyRollUp.profitBestAvailableBase < 0
                          ? "neg"
                          : undefined
                      }
                    >
                      {formatMoney(
                        result.data.baseCurrencyRollUp.profitBestAvailableBase,
                        result.data.baseCurrencyRollUp.baseCurrency
                      )}
                    </dd>
                  </div>
                </dl>
                <p className="note">{result.data.baseCurrencyRollUp.fxStubNote}</p>
              </>
            ) : null}

            <p className="note">{result.data.note}</p>

            <p className="cta-row">
              <Link className="btn" href="/queues/exceptions">
                Xử lý {exceptionQueueLabel}
              </Link>{" "}
              <Link className="btn btn-ghost" href="/queues/approvals">
                Xem {approvalQueueLabel}
              </Link>
            </p>
          </>
        )}
      </section>
    </AppShell>
  );
}
