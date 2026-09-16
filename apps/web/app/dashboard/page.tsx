import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import {
  FinColors,
  GroupedBarChart,
  StackedCompositionBar,
} from "@/components/charts/FinanceCharts";
import { AUTH_COOKIE, DISPLAY_NAME_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import {
  agingBucketLabel,
  getAgingSummary,
  type AgingBucketSummary,
} from "@/lib/ap-ar";
import {
  billTypeLabel,
  listBills,
  operationalStatusLabel,
} from "@/lib/bills";
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

function orderedBuckets(buckets: AgingBucketSummary[] | undefined) {
  const map = new Map((buckets ?? []).map((b) => [b.bucket.toLowerCase(), b]));
  return AGING_BUCKET_ORDER.map((key) => map.get(key)).filter(
    (b): b is AgingBucketSummary => Boolean(b)
  );
}

function weekdayVi(d: Date): string {
  const names = [
    "Chủ Nhật",
    "Thứ Hai",
    "Thứ Ba",
    "Thứ Tư",
    "Thứ Năm",
    "Thứ Sáu",
    "Thứ Bảy",
  ];
  return names[d.getDay()] ?? "";
}

function compactMoney(n: number, currency: string): string {
  const abs = Math.abs(n);
  const sign = n < 0 ? "−" : "";
  if (abs >= 1_000_000_000) return `${sign}${(abs / 1_000_000_000).toFixed(1)}B`;
  if (abs >= 1_000_000) return `${sign}${(abs / 1_000_000).toFixed(1)}M`;
  if (abs >= 1_000) return `${sign}${(abs / 1_000).toFixed(0)}k`;
  return formatMoney(n, currency);
}

export default async function DashboardPage() {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const displayName = jar.get(DISPLAY_NAME_COOKIE)?.value?.trim() || "";
  const terms = await fetchTerminology();
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
  const bankFeedLabel = term(terms, "BANK_FEED", "Sao kê ngân hàng");
  const docLabel = term(terms, "FINANCIAL_DOCUMENT", "Chứng từ tài chính");
  const apLabel = term(terms, "ACCOUNTS_PAYABLE", "Công nợ phải trả");
  const arLabel = term(terms, "ACCOUNTS_RECEIVABLE", "Công nợ phải thu");
  const expectedLabel = term(terms, "EXPECTED", "Dự kiến");
  const confirmedLabel = term(terms, "CONFIRMED", "Đã xác nhận");
  const actualLabel = term(terms, "ACTUAL", "Thực tế");
  const maturityLabel = term(terms, "MATURITY_BREAKDOWN", "Phân tách độ chín");
  const bankUnmatchedLabel = term(terms, "BANK_FEED_UNMATCHED", "Chưa đối soát");
  const agingLabel = term(terms, "AGING", "Tuổi nợ");

  const [result, agingRes, billsRes] = await Promise.all([
    getDashboardSummary(),
    getAgingSummary(),
    listBills(),
  ]);

  const now = new Date();
  const greetingName = displayName ? `Xin chào, ${displayName}!` : "Xin chào!";
  const dateLine = `${weekdayVi(now)}, ${now.toLocaleDateString("vi-VN")}`;

  const vis = result.ok
    ? result.data.financialVisibility ?? {
        canViewCost: true,
        canViewRevenue: true,
        canViewMargin: true,
      }
    : null;

  const roll = result.ok ? result.data.baseCurrencyRollUp : null;
  const row0 = result.ok ? result.data.totalsByCurrency[0] : null;
  const currency =
    roll?.baseCurrency ?? row0?.currencyCode ?? "VND";
  const costAmt = Number(
    roll?.costBestAvailableBase ?? row0?.costBestAvailable ?? 0
  );
  const revAmt = Number(
    roll?.revenueBestAvailableBase ?? row0?.revenueBestAvailable ?? 0
  );
  const profitAmt = Number(
    roll?.profitBestAvailableBase ?? row0?.profitBestAvailable ?? 0
  );

  const recentBills = billsRes.ok ? billsRes.data.items.slice(0, 6) : [];
  const mat = result.ok ? result.data.maturityPipeline : null;

  const notifs: { tone: "ok" | "warn" | "info"; text: string; href: string }[] =
    [];
  if (result.ok) {
    if (result.data.overdueExceptionCount > 0) {
      notifs.push({
        tone: "warn",
        text: `${result.data.overdueExceptionCount} ngoại lệ quá hạn cần xử lý`,
        href: "/queues/exceptions?overdueOnly=1",
      });
    }
    if (result.data.unmatchedBankFeedCount > 0) {
      notifs.push({
        tone: "warn",
        text: `${result.data.unmatchedBankFeedCount} dòng ${bankFeedLabel.toLowerCase()} chưa đối soát`,
        href: "/bank-feed?status=unmatched",
      });
    }
    if (result.data.pendingApprovalCount > 0) {
      notifs.push({
        tone: "info",
        text: `${result.data.pendingApprovalCount} yêu cầu chờ phê duyệt`,
        href: "/queues/approvals",
      });
    }
    if (result.data.openCloseCount > 0) {
      notifs.push({
        tone: "ok",
        text: `${result.data.openCloseCount} kỳ ${closeLabel.toLowerCase()} đang mở`,
        href: "/financial-closes",
      });
    }
  }

  const apBuckets = agingRes.ok
    ? orderedBuckets(agingRes.data.payable?.buckets)
    : [];
  const arBuckets = agingRes.ok
    ? orderedBuckets(agingRes.data.receivable?.buckets)
    : [];

  return (
    <AppShell terms={terms} active="dashboard">
      {!result.ok ? (
        <section className="panel panel-wide">
          <div className="alert alert-error" role="alert">
            {result.message}
          </div>
        </section>
      ) : (
        <div className="po-dash">
          {/* Hero */}
          <header className="po-dash-hero">
            <div>
              <p className="po-dash-hello">{greetingName}</p>
              <p className="po-dash-sub">
                Trung tâm điều hành tài chính quanh {billLabel} — việc cần xử lý,
                Best Available, chứng từ, AP/AR và chốt kỳ.
              </p>
            </div>
            <div className="po-dash-hero-meta">
              <p className="po-dash-date">{dateLine}</p>
              <p className="muted">
                {asOfLabel}: {formatDateTimeVi(result.data.asOfTimestamp)}
              </p>
              <p className="po-dash-slogan muted">
                Kiểm soát chi phí hôm nay · tạo lợi nhuận ngày mai
              </p>
            </div>
          </header>

          {/* KPI strip — no fake TMS "đơn hàng" (H-002) */}
          <div className="po-kpi-row" role="list">
            {vis?.canViewCost ? (
              <div className="po-kpi po-kpi-cost" role="listitem">
                <span className="po-kpi-icon" aria-hidden="true" />
                <span className="po-kpi-label">Tổng {costLabel.toLowerCase()}</span>
                <strong className="po-kpi-value">
                  {formatMoney(costAmt, currency)}
                </strong>
                <span className="po-kpi-hint">Best Available · {currency}</span>
              </div>
            ) : null}
            {vis?.canViewRevenue ? (
              <div className="po-kpi po-kpi-rev" role="listitem">
                <span className="po-kpi-icon" aria-hidden="true" />
                <span className="po-kpi-label">Tổng {revenueLabel.toLowerCase()}</span>
                <strong className="po-kpi-value">
                  {formatMoney(revAmt, currency)}
                </strong>
                <span className="po-kpi-hint">Best Available · {currency}</span>
              </div>
            ) : null}
            {vis?.canViewMargin ? (
              <div
                className={`po-kpi po-kpi-profit${profitAmt < 0 ? " is-neg" : ""}`}
                role="listitem"
              >
                <span className="po-kpi-icon" aria-hidden="true" />
                <span className="po-kpi-label">{profitLabel}</span>
                <strong className="po-kpi-value">
                  {formatMoney(profitAmt, currency)}
                </strong>
                <span className="po-kpi-hint">DT − CP (Best Available)</span>
              </div>
            ) : null}
            <Link className="po-kpi po-kpi-bill" href="/bills" role="listitem">
              <span className="po-kpi-icon" aria-hidden="true" />
              <span className="po-kpi-label">Số {billLabel}</span>
              <strong className="po-kpi-value">{result.data.billCount}</strong>
              <span className="po-kpi-hint">Neo tài chính trong phạm vi</span>
            </Link>
          </div>

          {/* Task strip */}
          <section className="po-task-panel" aria-labelledby="po-tasks">
            <div className="po-section-head">
              <h2 id="po-tasks">Việc cần xử lý của bạn</h2>
              <Link className="row-link" href="/control">
                Xem tất cả →
              </Link>
            </div>
            <div className="po-task-strip">
              <Link className="po-task" href="/queues/exceptions">
                <strong>{result.data.openExceptionCount}</strong>
                <span>{exceptionQueueLabel}</span>
              </Link>
              <Link className="po-task" href="/queues/approvals">
                <strong>{result.data.pendingApprovalCount}</strong>
                <span>Chờ phê duyệt</span>
              </Link>
              <Link
                className={`po-task${result.data.overdueExceptionCount > 0 ? " is-warn" : ""}`}
                href="/queues/exceptions?overdueOnly=1"
              >
                <strong>{result.data.overdueExceptionCount}</strong>
                <span>{overdueLabel}</span>
              </Link>
              <Link className="po-task" href="/queues/reconciliations">
                <strong>{result.data.openReconciliationCount}</strong>
                <span>{reconQueueLabel}</span>
              </Link>
              <Link className="po-task" href="/queues/variances">
                <strong>{result.data.openVarianceCount}</strong>
                <span>{openVarianceLabel}</span>
              </Link>
              <Link
                className={`po-task${result.data.unmatchedBankFeedCount > 0 ? " is-warn" : ""}`}
                href="/bank-feed?status=unmatched"
              >
                <strong>{result.data.unmatchedBankFeedCount}</strong>
                <span>
                  {bankFeedLabel} · {bankUnmatchedLabel}
                </span>
              </Link>
              <Link className="po-task" href="/financial-closes">
                <strong>{result.data.openCloseCount}</strong>
                <span>{closeLabel} đang mở</span>
              </Link>
              <Link className="po-task" href="/bills">
                <strong>{result.data.billCount}</strong>
                <span>{billLabel} cần theo dõi</span>
              </Link>
            </div>
          </section>

          {/* Chart + Best available */}
          <div className="po-mid-grid">
            <section className="po-card" aria-labelledby="po-chart">
              <div className="po-section-head">
                <h2 id="po-chart">
                  {costLabel}, {revenueLabel.toLowerCase()} và{" "}
                  {profitLabel.toLowerCase()}
                </h2>
                <span className="muted">Kỳ hiện tại · Best Available</span>
              </div>
              {(vis?.canViewCost || vis?.canViewRevenue) &&
              (costAmt !== 0 || revAmt !== 0 || profitAmt !== 0) ? (
                <GroupedBarChart
                  caption={`${bestAvailableLabel} (${currency})`}
                  series={[
                    ...(vis?.canViewCost
                      ? [
                          {
                            key: "cost",
                            label: costLabel,
                            value: costAmt,
                            color: "#3b82f6",
                          },
                        ]
                      : []),
                    ...(vis?.canViewRevenue
                      ? [
                          {
                            key: "rev",
                            label: revenueLabel,
                            value: revAmt,
                            color: "#22c55e",
                          },
                        ]
                      : []),
                    ...(vis?.canViewMargin
                      ? [
                          {
                            key: "pnl",
                            label: profitLabel,
                            value: profitAmt,
                            color:
                              profitAmt < 0 ? FinColors.profitNeg : "#f59e0b",
                          },
                        ]
                      : []),
                  ]}
                  valueFormatter={(n) => compactMoney(n, currency)}
                />
              ) : (
                <p className="empty-state" role="status">
                  Chưa có số CP/DT để vẽ. Ghi dòng trên {billLabel} rồi quay lại.
                </p>
              )}
            </section>

            <section className="po-card" aria-labelledby="po-ba">
              <div className="po-section-head">
                <h2 id="po-ba">{bestAvailableLabel}</h2>
                <span className="muted">{currency}</span>
              </div>
              <p className="cluster-lede" style={{ marginTop: 0 }}>
                {actualLabel} → {confirmedLabel} → {expectedLabel}. Projection —
                không phải sổ cái.
              </p>
              <div className="po-ba-stack">
                {vis?.canViewCost ? (
                  <div className="po-ba-block">
                    <div className="po-ba-head">
                      <span>{costLabel}</span>
                      <strong>{formatMoney(costAmt, currency)}</strong>
                    </div>
                    {mat ? (
                      <ul className="po-ba-maturity">
                        <li>
                          <span>{expectedLabel}</span>
                          <strong>{mat.costExpectedOnlyCount} dòng</strong>
                        </li>
                        <li>
                          <span>{confirmedLabel}</span>
                          <strong>{mat.costConfirmedOnlyCount} dòng</strong>
                        </li>
                        <li>
                          <span>{actualLabel}</span>
                          <strong>{mat.costActualCount} dòng</strong>
                        </li>
                      </ul>
                    ) : null}
                  </div>
                ) : null}
                {vis?.canViewRevenue ? (
                  <div className="po-ba-block">
                    <div className="po-ba-head">
                      <span>{revenueLabel}</span>
                      <strong>{formatMoney(revAmt, currency)}</strong>
                    </div>
                    {mat ? (
                      <ul className="po-ba-maturity">
                        <li>
                          <span>{expectedLabel}</span>
                          <strong>{mat.revenueExpectedOnlyCount} dòng</strong>
                        </li>
                        <li>
                          <span>{confirmedLabel}</span>
                          <strong>{mat.revenueConfirmedOnlyCount} dòng</strong>
                        </li>
                        <li>
                          <span>{actualLabel}</span>
                          <strong>{mat.revenueActualCount} dòng</strong>
                        </li>
                      </ul>
                    ) : null}
                  </div>
                ) : null}
                {vis?.canViewMargin ? (
                  <div className="po-ba-block">
                    <div className="po-ba-head">
                      <span>{profitLabel}</span>
                      <strong className={profitAmt < 0 ? "neg" : undefined}>
                        {formatMoney(profitAmt, currency)}
                      </strong>
                    </div>
                    <p className="muted" style={{ margin: "0.35rem 0 0", fontSize: "0.82rem" }}>
                      Biên = Best Available DT − CP
                    </p>
                  </div>
                ) : null}
              </div>
            </section>
          </div>

          {/* Maturity + recent bills */}
          <div className="po-mid-grid">
            <section className="po-card" aria-labelledby="po-mat">
              <div className="po-section-head">
                <h2 id="po-mat">{maturityLabel}</h2>
              </div>
              {mat && (vis?.canViewCost || vis?.canViewRevenue) ? (
                <div className="po-mat-cols">
                  {vis?.canViewCost ? (
                    <StackedCompositionBar
                      caption={`${costLabel} (số dòng)`}
                      segments={[
                        {
                          key: "e",
                          label: expectedLabel,
                          value: mat.costExpectedOnlyCount,
                          color: FinColors.expected,
                        },
                        {
                          key: "c",
                          label: confirmedLabel,
                          value: mat.costConfirmedOnlyCount,
                          color: FinColors.confirmed,
                        },
                        {
                          key: "a",
                          label: actualLabel,
                          value: mat.costActualCount,
                          color: FinColors.actual,
                        },
                      ]}
                    />
                  ) : null}
                  {vis?.canViewRevenue ? (
                    <StackedCompositionBar
                      caption={`${revenueLabel} (số dòng)`}
                      segments={[
                        {
                          key: "e",
                          label: expectedLabel,
                          value: mat.revenueExpectedOnlyCount,
                          color: FinColors.expected,
                        },
                        {
                          key: "c",
                          label: confirmedLabel,
                          value: mat.revenueConfirmedOnlyCount,
                          color: FinColors.confirmed,
                        },
                        {
                          key: "a",
                          label: actualLabel,
                          value: mat.revenueActualCount,
                          color: FinColors.actual,
                        },
                      ]}
                    />
                  ) : null}
                </div>
              ) : (
                <p className="empty-state" role="status">
                  Chưa có dữ liệu độ chín.
                </p>
              )}
            </section>

            <section className="po-card" aria-labelledby="po-recent">
              <div className="po-section-head">
                <h2 id="po-recent">{billLabel} gần đây</h2>
                <Link className="row-link" href="/bills">
                  Xem tất cả →
                </Link>
              </div>
              {recentBills.length === 0 ? (
                <p className="empty-state" role="status">
                  Chưa có {billLabel}.{" "}
                  <Link className="row-link" href="/bills/new">
                    Tạo mới
                  </Link>
                </p>
              ) : (
                <div className="table-wrap">
                  <table className="data-table po-recent-table">
                    <thead>
                      <tr>
                        <th scope="col">Mã {billLabel}</th>
                        <th scope="col">Loại</th>
                        <th scope="col">Ngày tạo</th>
                        <th scope="col">Trạng thái</th>
                      </tr>
                    </thead>
                    <tbody>
                      {recentBills.map((b) => (
                        <tr key={b.id}>
                          <td>
                            <Link className="row-link" href={`/bills/${b.id}`}>
                              {b.billNo}
                            </Link>
                          </td>
                          <td>{billTypeLabel(b.billType)}</td>
                          <td>{formatDateTimeVi(b.createdAt)}</td>
                          <td>
                            <span
                              className={`status-pill status-${b.operationalStatus?.toLowerCase() || "active"}`}
                            >
                              {operationalStatusLabel(b.operationalStatus)}
                            </span>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </section>
          </div>

          {/* Bottom: docs / AP-AR / notifications */}
          <div className="po-bottom-grid">
            <section className="po-card" aria-labelledby="po-docs">
              <div className="po-section-head">
                <h2 id="po-docs">{docLabel}</h2>
                <Link className="row-link" href="/documents">
                  Xem →
                </Link>
              </div>
              {result.data.documents ? (
                <ul className="po-mini-stats">
                  <li>
                    <span>Chờ chấp nhận</span>
                    <strong>{result.data.documents.awaitingAcceptanceCount}</strong>
                  </li>
                  <li>
                    <span>Đã chấp nhận · chưa khớp</span>
                    <strong>{result.data.documents.acceptedUnmatchedCount}</strong>
                  </li>
                  <li>
                    <span>Khớp nháp</span>
                    <strong>{result.data.documents.draftMatchCount}</strong>
                  </li>
                </ul>
              ) : (
                <p className="muted">Không có quyền xem cụm chứng từ.</p>
              )}
            </section>

            <section className="po-card" aria-labelledby="po-apar">
              <div className="po-section-head">
                <h2 id="po-apar">
                  {apLabel} / {arLabel}
                </h2>
                <Link className="row-link" href="/ap-ar">
                  Xem →
                </Link>
              </div>
              {result.data.apAr ? (
                <ul className="po-mini-stats">
                  <li>
                    <span>{apLabel} còn dư</span>
                    <strong>{result.data.apAr.openAccountsPayableCount}</strong>
                  </li>
                  <li>
                    <span>{arLabel} còn dư</span>
                    <strong>
                      {result.data.apAr.openAccountsReceivableCount}
                    </strong>
                  </li>
                </ul>
              ) : (
                <p className="muted">Không có quyền xem AP/AR.</p>
              )}
              {(apBuckets.length > 0 || arBuckets.length > 0) && (
                <p className="muted" style={{ marginTop: "0.65rem", fontSize: "0.8rem" }}>
                  {agingLabel}:{" "}
                  {[...apBuckets, ...arBuckets]
                    .slice(0, 3)
                    .map((b) => `${agingBucketLabel(b.bucket)} ${b.count}`)
                    .join(" · ")}
                </p>
              )}
            </section>

            <section className="po-card" aria-labelledby="po-notif">
              <div className="po-section-head">
                <h2 id="po-notif">Thông báo hệ thống</h2>
              </div>
              {notifs.length === 0 ? (
                <p className="muted" role="status">
                  Không có cảnh báo từ hàng đợi hiện tại.
                </p>
              ) : (
                <ul className="po-notif-list">
                  {notifs.map((n) => (
                    <li key={n.href + n.text} className={`tone-${n.tone}`}>
                      <Link href={n.href}>{n.text}</Link>
                    </li>
                  ))}
                </ul>
              )}
            </section>
          </div>

          <p className="note po-dash-note">{result.data.note}</p>
        </div>
      )}
    </AppShell>
  );
}
