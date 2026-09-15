import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import {
  FinColors,
  FunnelSteps,
  GroupedBarChart,
  HorizontalBarChart,
  StackedCompositionBar,
} from "@/components/charts/FinanceCharts";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import {
  agingBucketLabel,
  getAgingSummary,
  type AgingBucketSummary,
} from "@/lib/ap-ar";
import { getDashboardSummary } from "@/lib/control-desk";
import { formatDateTimeVi, formatMoney } from "@/lib/money";

const AGING_BUCKET_ORDER = [
  "current",
  "1_30",
  "31_60",
  "61_90",
  "90_plus",
  "no_due_date",
] as const;

function agingColor(bucket: string): string {
  switch (bucket.toLowerCase()) {
    case "current":
      return FinColors.agingCurrent;
    case "1_30":
      return FinColors.agingMid;
    case "31_60":
      return FinColors.agingMid;
    case "61_90":
    case "90_plus":
      return FinColors.agingLate;
    default:
      return FinColors.agingNone;
  }
}

function orderedBuckets(buckets: AgingBucketSummary[] | undefined) {
  const map = new Map((buckets ?? []).map((b) => [b.bucket.toLowerCase(), b]));
  return AGING_BUCKET_ORDER.map((key) => map.get(key)).filter(
    (b): b is AgingBucketSummary => Boolean(b)
  );
}

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
  const docLabel = term(terms, "FINANCIAL_DOCUMENT", "Chứng từ tài chính");
  const matchLabel = term(terms, "DOCUMENT_MATCH", "Khớp chứng từ");
  const apLabel = term(terms, "ACCOUNTS_PAYABLE", "Khoản phải trả");
  const arLabel = term(terms, "ACCOUNTS_RECEIVABLE", "Khoản phải thu");
  const payableExposureLabel = term(
    terms,
    "PAYABLE_EXPOSURE",
    "Nghĩa vụ phải trả (exposure)"
  );
  const receivableExposureLabel = term(
    terms,
    "RECEIVABLE_EXPOSURE",
    "Quyền thu dự kiến (exposure)"
  );
  const paymentLabel = term(terms, "PAYMENT", "Thanh toán");
  const collectionLabel = term(terms, "COLLECTION", "Thu tiền");
  const expectedLabel = term(terms, "EXPECTED", "Dự kiến");
  const confirmedLabel = term(terms, "CONFIRMED", "Đã xác nhận");
  const actualLabel = term(terms, "ACTUAL", "Thực tế");
  const maturityLabel = term(terms, "MATURITY_BREAKDOWN", "Phân tách độ chín");
  const settlementOpenLabel = term(terms, "SETTLEMENT_OPEN", "Chưa tất toán");
  const bankUnmatchedLabel = term(terms, "BANK_FEED_UNMATCHED", "Chưa đối soát");
  const receivedLabel = term(terms, "RECEIVED", "Đã nhận");
  const acceptedLabel = term(terms, "ACCEPTED", "Đã chấp nhận");
  const matchedLabel = term(terms, "MATCHED", "Đã khớp");

  const [result, agingRes] = await Promise.all([
    getDashboardSummary(),
    getAgingSummary(),
  ]);
  const vis = result.ok
    ? result.data.financialVisibility ?? {
        canViewCost: true,
        canViewRevenue: true,
        canViewMargin: true,
      }
    : null;
  const agingLabel = term(terms, "AGING", "Tuổi nợ");

  function moneyOrHidden(
    amount: number | null | undefined,
    currency: string,
    allowed: boolean
  ): string {
    if (!allowed || amount == null) return "—";
    return formatMoney(amount, currency);
  }

  return (
    <AppShell terms={terms} active="dashboard">
      <section className="panel panel-wide dash-hero">
        <h1>{summaryLabel}</h1>
        <p className="lede">
          {dashboardLabel} kiểm soát tài chính quanh {billLabel}: việc cần xử lý,
          {` ${bestAvailableLabel}`}, chứng từ, AP/AR, tất toán và chốt — số liệu
          projection, không phải sổ cái.
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
            <p className="cta-row">
              <Link className="btn" href="/queues/exceptions">
                Xử lý {exceptionQueueLabel}
              </Link>{" "}
              <Link className="btn btn-ghost" href="/bills">
                Mở danh sách {billLabel}
              </Link>
            </p>
          </>
        )}
      </section>

      {result.ok ? (
        <div className="dash-layout">
          {/* —— 1. Việc cần xử lý —— */}
          <section className="panel panel-wide dash-cluster" aria-labelledby="dash-work">
            <h2 id="dash-work" className="cluster-title">
              Việc cần xử lý
            </h2>
            <p className="cluster-lede">
              Hàng đợi kiểm soát: ngoại lệ, phê duyệt, đối soát, chênh lệch.
            </p>
            <div className="stat-grid" role="list">
              <Link
                className="stat-card"
                href="/queues/exceptions"
                role="listitem"
              >
                <span className="stat-label">{exceptionQueueLabel}</span>
                <span className="stat-value">
                  {result.data.openExceptionCount}
                </span>
                <span className="stat-hint">Mở hàng đợi ngoại lệ</span>
              </Link>
              <Link
                className="stat-card"
                href="/queues/approvals"
                role="listitem"
              >
                <span className="stat-label">{approvalQueueLabel}</span>
                <span className="stat-value">
                  {result.data.pendingApprovalCount}
                </span>
                <span className="stat-hint">Mở hàng đợi phê duyệt</span>
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
                  <Link
                    className="stat-hint row-link"
                    href="/queues/exceptions?overdueOnly=1"
                  >
                    Xem quá hạn
                  </Link>
                ) : (
                  <span className="stat-hint">Không có quá hạn</span>
                )}
              </div>
              <Link
                className="stat-card"
                href="/queues/reconciliations"
                role="listitem"
              >
                <span className="stat-label">{reconQueueLabel}</span>
                <span className="stat-value">
                  {result.data.openReconciliationCount}
                </span>
                <span className="stat-hint">Phiên draft / đang xử lý</span>
              </Link>
              <Link
                className="stat-card"
                href="/queues/variances"
                role="listitem"
              >
                <span className="stat-label">{openVarianceLabel}</span>
                <span className="stat-value">
                  {result.data.openVarianceCount}
                </span>
                <span className="stat-hint">Mở hàng đợi chênh lệch</span>
              </Link>
              <Link
                className="stat-card"
                href="/bank-feed?status=unmatched"
                role="listitem"
              >
                <span className="stat-label">
                  {bankFeedLabel} · {bankUnmatchedLabel}
                </span>
                <span
                  className={
                    result.data.unmatchedBankFeedCount > 0
                      ? "stat-value stat-warn"
                      : "stat-value"
                  }
                >
                  {result.data.unmatchedBankFeedCount}
                </span>
                <span className="stat-hint">Dòng chưa đối soát</span>
              </Link>
              <div className="stat-card stat-card-static" role="listitem">
                <span className="stat-label">{closeLabel} đang mở</span>
                <span className="stat-value">{result.data.openCloseCount}</span>
                <Link className="stat-hint row-link" href="/financial-closes">
                  Mở sổ chốt
                </Link>
              </div>
              <div className="stat-card stat-card-static" role="listitem">
                <span className="stat-label">Số {billLabel}</span>
                <span className="stat-value">{result.data.billCount}</span>
                <Link className="stat-hint row-link" href="/bills">
                  Mở danh sách {billLabel}
                </Link>
              </div>
            </div>
          </section>

          {/* —— 2. Best Available P&L —— */}
          <section
            className="panel panel-wide dash-cluster"
            aria-labelledby="dash-pnl"
          >
            <h2 id="dash-pnl" className="cluster-title">
              {bestAvailableLabel} · {costLabel} / {revenueLabel} / {profitLabel}
            </h2>
            <p className="cluster-lede">
              Theo tiền tệ: Actual → Confirmed → Expected. Projection read-only —
              không ghi lên {billLabel}. Xem chi phí ≠ xem doanh thu ≠ biên.
            </p>

            {vis && !vis.canViewCost && !vis.canViewRevenue ? (
              <div className="empty-state" role="status">
                Không có quyền xem {costLabel.toLowerCase()} /{" "}
                {revenueLabel.toLowerCase()}. Số tiền đã ẩn.
              </div>
            ) : result.data.totalsByCurrency.length === 0 ? (
              <div className="empty-state" role="status">
                Chưa có {costLabel}/{revenueLabel} để tổng hợp. Tạo dòng trên{" "}
                {billLabel} rồi quay lại.
              </div>
            ) : (
              <div className="table-wrap">
                <table className="data-table">
                  <thead>
                    <tr>
                      <th scope="col">Tiền tệ</th>
                      {vis?.canViewCost ? (
                        <th scope="col" className="num">
                          {costLabel}
                        </th>
                      ) : null}
                      {vis?.canViewRevenue ? (
                        <th scope="col" className="num">
                          {revenueLabel}
                        </th>
                      ) : null}
                      {vis?.canViewMargin ? (
                        <th scope="col" className="num">
                          {profitLabel}
                        </th>
                      ) : null}
                    </tr>
                  </thead>
                  <tbody>
                    {result.data.totalsByCurrency.map((row) => (
                      <tr key={row.currencyCode}>
                        <td>{row.currencyCode}</td>
                        {vis?.canViewCost ? (
                          <td className="num">
                            {moneyOrHidden(
                              row.costBestAvailable,
                              row.currencyCode,
                              true
                            )}
                          </td>
                        ) : null}
                        {vis?.canViewRevenue ? (
                          <td className="num">
                            {moneyOrHidden(
                              row.revenueBestAvailable,
                              row.currencyCode,
                              true
                            )}
                          </td>
                        ) : null}
                        {vis?.canViewMargin ? (
                          <td
                            className={
                              (row.profitBestAvailable ?? 0) < 0
                                ? "num neg"
                                : "num"
                            }
                          >
                            {moneyOrHidden(
                              row.profitBestAvailable,
                              row.currencyCode,
                              true
                            )}
                          </td>
                        ) : null}
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}

            {result.data.baseCurrencyRollUp &&
            (vis?.canViewCost || vis?.canViewRevenue) ? (
              <>
                <h3 className="section-title sm">{rollUpLabel}</h3>
                <dl className="metric-grid">
                  <div>
                    <dt>Tiền tệ cơ sở</dt>
                    <dd>{result.data.baseCurrencyRollUp.baseCurrency}</dd>
                  </div>
                  {vis?.canViewCost ? (
                    <div>
                      <dt>{costLabel}</dt>
                      <dd>
                        {moneyOrHidden(
                          result.data.baseCurrencyRollUp.costBestAvailableBase,
                          result.data.baseCurrencyRollUp.baseCurrency,
                          true
                        )}
                      </dd>
                    </div>
                  ) : null}
                  {vis?.canViewRevenue ? (
                    <div>
                      <dt>{revenueLabel}</dt>
                      <dd>
                        {moneyOrHidden(
                          result.data.baseCurrencyRollUp
                            .revenueBestAvailableBase,
                          result.data.baseCurrencyRollUp.baseCurrency,
                          true
                        )}
                      </dd>
                    </div>
                  ) : null}
                  {vis?.canViewMargin ? (
                    <div>
                      <dt>{profitLabel}</dt>
                      <dd
                        className={
                          (result.data.baseCurrencyRollUp
                            .profitBestAvailableBase ?? 0) < 0
                            ? "neg"
                            : undefined
                        }
                      >
                        {moneyOrHidden(
                          result.data.baseCurrencyRollUp
                            .profitBestAvailableBase,
                          result.data.baseCurrencyRollUp.baseCurrency,
                          true
                        )}
                      </dd>
                    </div>
                  ) : null}
                </dl>
                <p className="note">
                  {result.data.baseCurrencyRollUp.fxStubNote}
                </p>
              </>
            ) : null}

            <p className="note">{result.data.note}</p>
          </section>

          {/* —— 2b. Biểu đồ kiểm soát —— */}
          <section
            className="panel panel-wide dash-cluster dash-span-2"
            aria-labelledby="dash-charts"
          >
            <h2 id="dash-charts" className="cluster-title">
              Biểu đồ kiểm soát
            </h2>
            <p className="cluster-lede">
              P&amp;L Best Available, độ chín dòng, hàng đợi việc, phễu chứng từ và{" "}
              {agingLabel.toLowerCase()} AP/AR — cùng nguồn API, không SoT.
            </p>

            <div className="fin-chart-grid">
              {(vis?.canViewCost || vis?.canViewRevenue) &&
              (result.data.baseCurrencyRollUp ||
                result.data.totalsByCurrency[0]) ? (
                <GroupedBarChart
                  caption={`${bestAvailableLabel} (${
                    result.data.baseCurrencyRollUp?.baseCurrency ??
                    result.data.totalsByCurrency[0]?.currencyCode ??
                    ""
                  })`}
                  series={[
                    ...(vis?.canViewCost
                      ? [
                          {
                            key: "cost",
                            label: costLabel,
                            value: Number(
                              result.data.baseCurrencyRollUp
                                ?.costBestAvailableBase ??
                                result.data.totalsByCurrency[0]
                                  ?.costBestAvailable ??
                                0
                            ),
                            color: FinColors.cost,
                          },
                        ]
                      : []),
                    ...(vis?.canViewRevenue
                      ? [
                          {
                            key: "rev",
                            label: revenueLabel,
                            value: Number(
                              result.data.baseCurrencyRollUp
                                ?.revenueBestAvailableBase ??
                                result.data.totalsByCurrency[0]
                                  ?.revenueBestAvailable ??
                                0
                            ),
                            color: FinColors.revenue,
                          },
                        ]
                      : []),
                    ...(vis?.canViewMargin
                      ? [
                          {
                            key: "pnl",
                            label: profitLabel,
                            value: Number(
                              result.data.baseCurrencyRollUp
                                ?.profitBestAvailableBase ??
                                result.data.totalsByCurrency[0]
                                  ?.profitBestAvailable ??
                                0
                            ),
                            color:
                              Number(
                                result.data.baseCurrencyRollUp
                                  ?.profitBestAvailableBase ??
                                  result.data.totalsByCurrency[0]
                                    ?.profitBestAvailable ??
                                  0
                              ) < 0
                                ? FinColors.profitNeg
                                : FinColors.profit,
                          },
                        ]
                      : []),
                  ]}
                  valueFormatter={(n) => {
                    const abs = Math.abs(n);
                    const sign = n < 0 ? "−" : "";
                    if (abs >= 1_000_000_000)
                      return `${sign}${(abs / 1_000_000_000).toFixed(1)}B`;
                    if (abs >= 1_000_000)
                      return `${sign}${(abs / 1_000_000).toFixed(1)}M`;
                    if (abs >= 1_000)
                      return `${sign}${(abs / 1_000).toFixed(0)}k`;
                    return formatMoney(
                      n,
                      result.data.baseCurrencyRollUp?.baseCurrency ??
                        result.data.totalsByCurrency[0]?.currencyCode ??
                        "VND"
                    );
                  }}
                />
              ) : (
                <div className="fin-chart">
                  <p className="fin-chart-caption">
                    {bestAvailableLabel} · P&amp;L
                  </p>
                  <p className="fin-chart-empty" role="status">
                    Không có số tiền để vẽ (thiếu quyền hoặc chưa có dòng CP/DT).
                  </p>
                </div>
              )}

              {result.data.maturityPipeline && vis?.canViewCost ? (
                <StackedCompositionBar
                  caption={`${maturityLabel} · ${costLabel} (số dòng)`}
                  segments={[
                    {
                      key: "e",
                      label: expectedLabel,
                      value:
                        result.data.maturityPipeline.costExpectedOnlyCount,
                      color: FinColors.expected,
                    },
                    {
                      key: "c",
                      label: confirmedLabel,
                      value:
                        result.data.maturityPipeline.costConfirmedOnlyCount,
                      color: FinColors.confirmed,
                    },
                    {
                      key: "a",
                      label: actualLabel,
                      value: result.data.maturityPipeline.costActualCount,
                      color: FinColors.actual,
                    },
                  ]}
                />
              ) : null}

              {result.data.maturityPipeline && vis?.canViewRevenue ? (
                <StackedCompositionBar
                  caption={`${maturityLabel} · ${revenueLabel} (số dòng)`}
                  segments={[
                    {
                      key: "e",
                      label: expectedLabel,
                      value:
                        result.data.maturityPipeline
                          .revenueExpectedOnlyCount,
                      color: FinColors.expected,
                    },
                    {
                      key: "c",
                      label: confirmedLabel,
                      value:
                        result.data.maturityPipeline
                          .revenueConfirmedOnlyCount,
                      color: FinColors.confirmed,
                    },
                    {
                      key: "a",
                      label: actualLabel,
                      value: result.data.maturityPipeline.revenueActualCount,
                      color: FinColors.actual,
                    },
                  ]}
                />
              ) : null}

              <HorizontalBarChart
                caption="Cơ cấu hàng đợi việc"
                series={[
                  {
                    key: "ex",
                    label: exceptionQueueLabel,
                    value: result.data.openExceptionCount,
                    color: FinColors.work,
                  },
                  {
                    key: "od",
                    label: overdueLabel,
                    value: result.data.overdueExceptionCount,
                    color: FinColors.workDanger,
                  },
                  {
                    key: "appr",
                    label: approvalQueueLabel,
                    value: result.data.pendingApprovalCount,
                    color: FinColors.workMuted,
                  },
                  {
                    key: "var",
                    label: openVarianceLabel,
                    value: result.data.openVarianceCount,
                    color: FinColors.workWarn,
                  },
                  {
                    key: "recon",
                    label: reconQueueLabel,
                    value: result.data.openReconciliationCount,
                    color: FinColors.work,
                  },
                  {
                    key: "bank",
                    label: `${bankFeedLabel} · ${bankUnmatchedLabel}`,
                    value: result.data.unmatchedBankFeedCount,
                    color: FinColors.workWarn,
                  },
                ]}
              />

              {result.data.documents ? (
                <FunnelSteps
                  caption={`Phễu ${docLabel.toLowerCase()}`}
                  steps={[
                    {
                      key: "recv",
                      label: `${receivedLabel}, chờ chấp nhận`,
                      value: result.data.documents.awaitingAcceptanceCount,
                      color: FinColors.doc1,
                    },
                    {
                      key: "acc",
                      label: `${acceptedLabel}, chưa khớp đủ`,
                      value: result.data.documents.acceptedUnmatchedCount,
                      color: FinColors.doc2,
                    },
                    {
                      key: "draft",
                      label: `${matchLabel} nháp`,
                      value: result.data.documents.draftMatchCount,
                      color: FinColors.doc3,
                    },
                  ]}
                />
              ) : null}

              {agingRes.ok &&
              (agingRes.data.canViewPayable ||
                agingRes.data.canViewReceivable) ? (
                <>
                  {agingRes.data.canViewPayable && agingRes.data.payable ? (
                    <HorizontalBarChart
                      caption={`${agingLabel} · ${apLabel} (dư nợ)`}
                      series={orderedBuckets(agingRes.data.payable.buckets).map(
                        (b) => ({
                          key: b.bucket,
                          label: agingBucketLabel(b.bucket),
                          value: b.outstanding,
                          color: agingColor(b.bucket),
                        })
                      )}
                      valueFormatter={(n) =>
                        formatMoney(
                          n,
                          agingRes.data.payable?.payableItems?.[0]
                            ?.currencyCode || "VND"
                        )
                      }
                      emptyLabel="Không có dư nợ AP trong phạm vi."
                    />
                  ) : null}
                  {agingRes.data.canViewReceivable &&
                  agingRes.data.receivable ? (
                    <HorizontalBarChart
                      caption={`${agingLabel} · ${arLabel} (dư nợ)`}
                      series={orderedBuckets(
                        agingRes.data.receivable.buckets
                      ).map((b) => ({
                        key: b.bucket,
                        label: agingBucketLabel(b.bucket),
                        value: b.outstanding,
                        color: agingColor(b.bucket),
                      }))}
                      valueFormatter={(n) =>
                        formatMoney(
                          n,
                          agingRes.data.receivable?.receivableItems?.[0]
                            ?.currencyCode || "VND"
                        )
                      }
                      emptyLabel="Không có dư nợ AR trong phạm vi."
                    />
                  ) : null}
                </>
              ) : agingRes.ok ? (
                <div className="fin-chart">
                  <p className="fin-chart-caption">{agingLabel} AP/AR</p>
                  <p className="fin-chart-empty" role="status">
                    {agingRes.data.note}
                  </p>
                </div>
              ) : (
                <div className="fin-chart">
                  <p className="fin-chart-caption">{agingLabel} AP/AR</p>
                  <p className="fin-chart-empty" role="status">
                    {agingRes.message}
                  </p>
                </div>
              )}
            </div>

            <p className="cta-row">
              <Link className="btn btn-ghost" href="/ap-ar/aging">
                Chi tiết {agingLabel.toLowerCase()}
              </Link>
            </p>
          </section>

          {/* —— 3. Độ chín —— */}
          {result.data.maturityPipeline ? (
            <section
              className="panel panel-wide dash-cluster"
              aria-labelledby="dash-maturity"
            >
              <h2 id="dash-maturity" className="cluster-title">
                {maturityLabel}
              </h2>
              <p className="cluster-lede">
                Số dòng {costLabel}/{revenueLabel} theo lớp độ chín — không phải
                số tiền. Giúp thấy backlog xác nhận / thực tế hóa.
              </p>
              <div className="dash-split">
                <div>
                  <h3 className="section-title sm">{costLabel}</h3>
                  <div className="stat-grid stat-grid-3" role="list">
                    <div className="stat-card stat-card-static" role="listitem">
                      <span className="stat-label">{expectedLabel}</span>
                      <span className="stat-value">
                        {result.data.maturityPipeline.costExpectedOnlyCount}
                      </span>
                      <span className="stat-hint">Chỉ dự kiến</span>
                    </div>
                    <div className="stat-card stat-card-static" role="listitem">
                      <span className="stat-label">{confirmedLabel}</span>
                      <span className="stat-value">
                        {result.data.maturityPipeline.costConfirmedOnlyCount}
                      </span>
                      <span className="stat-hint">Đã xác nhận, chưa thực tế</span>
                    </div>
                    <div className="stat-card stat-card-static" role="listitem">
                      <span className="stat-label">{actualLabel}</span>
                      <span className="stat-value">
                        {result.data.maturityPipeline.costActualCount}
                      </span>
                      <span className="stat-hint">Đã có thực tế</span>
                    </div>
                  </div>
                </div>
                <div>
                  <h3 className="section-title sm">{revenueLabel}</h3>
                  <div className="stat-grid stat-grid-3" role="list">
                    <div className="stat-card stat-card-static" role="listitem">
                      <span className="stat-label">{expectedLabel}</span>
                      <span className="stat-value">
                        {
                          result.data.maturityPipeline
                            .revenueExpectedOnlyCount
                        }
                      </span>
                      <span className="stat-hint">Chỉ dự kiến</span>
                    </div>
                    <div className="stat-card stat-card-static" role="listitem">
                      <span className="stat-label">{confirmedLabel}</span>
                      <span className="stat-value">
                        {
                          result.data.maturityPipeline
                            .revenueConfirmedOnlyCount
                        }
                      </span>
                      <span className="stat-hint">Đã xác nhận, chưa thực tế</span>
                    </div>
                    <div className="stat-card stat-card-static" role="listitem">
                      <span className="stat-label">{actualLabel}</span>
                      <span className="stat-value">
                        {result.data.maturityPipeline.revenueActualCount}
                      </span>
                      <span className="stat-hint">Đã có thực tế</span>
                    </div>
                  </div>
                </div>
              </div>
            </section>
          ) : null}

          {/* —— 4. Chứng từ —— */}
          {result.data.documents ? (
            <section
              className="panel panel-wide dash-cluster"
              aria-labelledby="dash-docs"
            >
              <h2 id="dash-docs" className="cluster-title">
                {docLabel}
              </h2>
              <p className="cluster-lede">
                Chuỗi độc lập: {receivedLabel} ≠ {acceptedLabel} ≠ {matchedLabel}.
                Không tự tạo {costLabel}/{revenueLabel}.
              </p>
              <div className="stat-grid" role="list">
                <Link className="stat-card" href="/documents" role="listitem">
                  <span className="stat-label">
                    {receivedLabel}, chờ {acceptedLabel.toLowerCase()}
                  </span>
                  <span className="stat-value">
                    {result.data.documents.awaitingAcceptanceCount}
                  </span>
                  <span className="stat-hint">Mở danh sách chứng từ</span>
                </Link>
                <Link className="stat-card" href="/documents" role="listitem">
                  <span className="stat-label">
                    {acceptedLabel}, chưa khớp đủ
                  </span>
                  <span
                    className={
                      result.data.documents.acceptedUnmatchedCount > 0
                        ? "stat-value stat-warn"
                        : "stat-value"
                    }
                  >
                    {result.data.documents.acceptedUnmatchedCount}
                  </span>
                  <span className="stat-hint">Cần {matchLabel.toLowerCase()}</span>
                </Link>
                <div className="stat-card stat-card-static" role="listitem">
                  <span className="stat-label">
                    {matchLabel} nháp
                  </span>
                  <span className="stat-value">
                    {result.data.documents.draftMatchCount}
                  </span>
                  <Link className="stat-hint row-link" href="/documents">
                    Tiếp tục khớp
                  </Link>
                </div>
              </div>
              <p className="cta-row">
                <Link className="btn btn-ghost" href="/documents/receive">
                  Nhận chứng từ mới
                </Link>
              </p>
            </section>
          ) : null}

          {/* —— 5. AP/AR —— */}
          {result.data.apAr ? (
            <section
              className="panel panel-wide dash-cluster"
              aria-labelledby="dash-apar"
            >
              <h2 id="dash-apar" className="cluster-title">
                {apLabel} / {arLabel}
              </h2>
              <p className="cluster-lede">
                Exposure → ghi nhận AP/AR → tất toán. {settlementOpenLabel} gồm cả
                tất toán một phần.
              </p>
              <div className="stat-grid" role="list">
                <Link className="stat-card" href="/ap-ar" role="listitem">
                  <span className="stat-label">
                    {apLabel} · {settlementOpenLabel}
                  </span>
                  <span className="stat-value">
                    {result.data.apAr.openAccountsPayableCount}
                  </span>
                  <span className="stat-hint">Chưa tất toán hết</span>
                </Link>
                <Link className="stat-card" href="/ap-ar" role="listitem">
                  <span className="stat-label">
                    {arLabel} · {settlementOpenLabel}
                  </span>
                  <span className="stat-value">
                    {result.data.apAr.openAccountsReceivableCount}
                  </span>
                  <span className="stat-hint">Chưa tất toán hết</span>
                </Link>
                <Link
                  className="stat-card"
                  href="/ap-ar/exposures/new"
                  role="listitem"
                >
                  <span className="stat-label">{payableExposureLabel}</span>
                  <span className="stat-value">
                    {result.data.apAr.openPayableExposureCount}
                  </span>
                  <span className="stat-hint">Chưa ghi nhận đủ</span>
                </Link>
                <Link className="stat-card" href="/ap-ar" role="listitem">
                  <span className="stat-label">{receivableExposureLabel}</span>
                  <span className="stat-value">
                    {result.data.apAr.openReceivableExposureCount}
                  </span>
                  <span className="stat-hint">Chưa ghi nhận đủ</span>
                </Link>
              </div>
            </section>
          ) : null}

          {/* —— 6. Thanh toán / Thu tiền —— */}
          {result.data.settlements ? (
            <section
              className="panel panel-wide dash-cluster"
              aria-labelledby="dash-settle"
            >
              <h2 id="dash-settle" className="cluster-title">
                {paymentLabel} / {collectionLabel}
              </h2>
              <p className="cluster-lede">
                Giao dịch tiền mặt đang mở (chưa hủy). Phân bổ mới làm đổi số dư
                AP/AR.
              </p>
              <div className="stat-grid" role="list">
                <Link
                  className="stat-card"
                  href="/settlements"
                  role="listitem"
                >
                  <span className="stat-label">
                    {paymentLabel} đang mở
                  </span>
                  <span className="stat-value">
                    {result.data.settlements.openPaymentCount}
                  </span>
                  <span className="stat-hint">Danh sách tất toán</span>
                </Link>
                <Link
                  className="stat-card"
                  href="/settlements"
                  role="listitem"
                >
                  <span className="stat-label">
                    {collectionLabel} đang mở
                  </span>
                  <span className="stat-value">
                    {result.data.settlements.openCollectionCount}
                  </span>
                  <span className="stat-hint">Danh sách tất toán</span>
                </Link>
              </div>
              <p className="cta-row">
                <Link className="btn btn-ghost" href="/settlements/payments/new">
                  Tạo {paymentLabel.toLowerCase()}
                </Link>{" "}
                <Link
                  className="btn btn-ghost"
                  href="/settlements/collections/new"
                >
                  Tạo {collectionLabel.toLowerCase()}
                </Link>
              </p>
            </section>
          ) : null}

          {/* —— 7. Lối tắt chức năng —— */}
          <section
            className="panel panel-wide dash-cluster"
            aria-labelledby="dash-shortcuts"
          >
            <h2 id="dash-shortcuts" className="cluster-title">
              Lối tắt chức năng
            </h2>
            <p className="cluster-lede">
              Đi thẳng tới màn hình vận hành — không giả lập báo cáo.
            </p>
            <ul className="dash-shortcuts">
              <li>
                <Link href="/bills/new">Tạo {billLabel} mới</Link>
              </li>
              <li>
                <Link href="/costs/shared">
                  {costLabel} {term(terms, "ATTRIBUTION_SHARED", "Chung").toLowerCase()}
                </Link>
              </li>
              <li>
                <Link href="/rate-cards">Bảng giá / Rating</Link>
              </li>
              <li>
                <Link href="/documents/receive">Nhận {docLabel.toLowerCase()}</Link>
              </li>
              <li>
                <Link href="/ap-ar">Xem {apLabel} / {arLabel}</Link>
              </li>
              <li>
                <Link href="/ap-ar/aging">Tóm tắt tuổi nợ</Link>
              </li>
              <li>
                <Link href="/settlements">
                  {paymentLabel} &amp; {collectionLabel}
                </Link>
              </li>
              <li>
                <Link href="/reconciliations">Phiên đối soát</Link>
              </li>
              <li>
                <Link href="/bank-feed">{bankFeedLabel}</Link>
              </li>
              <li>
                <Link href="/financial-closes">{closeLabel}</Link>
              </li>
              <li>
                <Link href="/queues/approvals">{approvalQueueLabel}</Link>
              </li>
              <li>
                <Link href="/queues/variances">{openVarianceLabel}</Link>
              </li>
            </ul>
          </section>
        </div>
      ) : null}
    </AppShell>
  );
}
