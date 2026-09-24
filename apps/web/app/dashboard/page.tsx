import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { AUTH_COOKIE, DISPLAY_NAME_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import {
  billTypeLabel,
  listBills,
  operationalStatusLabel,
} from "@/lib/bills";
import { getDashboardSummary } from "@/lib/control-desk";
import { getTenantReadiness } from "@/lib/tenant-admin";
import { TenantReadinessPanel } from "@/components/TenantReadinessPanel";
import { formatMoney } from "@/lib/money";
import { listOrders } from "@/lib/operational-refs";

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

function statusPillClass(status: string): string {
  const s = (status || "").toLowerCase();
  if (s.includes("confirm") || s === "active") return "po-pill ok";
  if (s.includes("pending") || s.includes("await") || s.includes("document"))
    return "po-pill warn";
  return "po-pill";
}

/** Twelve months of Best Available by effective date. A missing amount is an empty month, not zero. */
function YearBars({
  points,
  canCost,
  canRev,
}: {
  points: { month: number; costBestAvailable: number | null; revenueBestAvailable: number | null }[];
  canCost: boolean;
  canRev: boolean;
}) {
  const magnitudes = points.flatMap((p) => [
    canCost && p.costBestAvailable != null ? Math.abs(p.costBestAvailable) : 0,
    canRev && p.revenueBestAvailable != null ? Math.abs(p.revenueBestAvailable) : 0,
  ]);
  const max = Math.max(1, ...magnitudes);

  return (
    <div className="po-bars" aria-hidden="true">
      {Array.from({ length: 12 }, (_, i) => {
        const point = points.find((p) => p.month === i + 1);
        const cost = canCost ? point?.costBestAvailable ?? null : null;
        const rev = canRev ? point?.revenueBestAvailable ?? null : null;
        const costH =
          cost != null && cost !== 0
            ? Math.max(4, Math.round((Math.abs(cost) / max) * 88))
            : 0;
        const revH =
          rev != null && rev !== 0
            ? Math.max(4, Math.round((Math.abs(rev) / max) * 88))
            : 0;
        return (
          <div className="po-month" key={i}>
            {cost != null ? (
              <i className="po-bar b1" style={{ height: `${costH}%` }} />
            ) : null}
            {rev != null ? (
              <i className="po-bar b2" style={{ height: `${revH}%` }} />
            ) : null}
            <span>Th{String(i + 1).padStart(2, "0")}</span>
          </div>
        );
      })}
    </div>
  );
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
  const bestAvailableLabel = term(terms, "BEST_AVAILABLE", "Giá trị tốt nhất hiện có");
  const costLabel = term(terms, "COST", "Chi phí");
  const revenueLabel = term(terms, "REVENUE", "Doanh thu");
  const profitLabel = term(terms, "PROFIT", "Lợi nhuận");
  const closeLabel = term(terms, "FINANCIAL_CLOSE", "Chốt tài chính");
  const bankFeedLabel = term(terms, "BANK_FEED", "Sao kê ngân hàng");
  const docLabel = term(terms, "FINANCIAL_DOCUMENT", "Chứng từ tài chính");
  const apLabel = term(terms, "ACCOUNTS_PAYABLE", "Công nợ phải trả");
  const arLabel = term(terms, "ACCOUNTS_RECEIVABLE", "Công nợ phải thu");
  const expectedLabel = term(terms, "EXPECTED", "Dự kiến");
  const confirmedLabel = term(terms, "CONFIRMED", "Đã xác nhận");
  const actualLabel = term(terms, "ACTUAL", "Thực tế");
  const maturityLabel = term(terms, "MATURITY_BREAKDOWN", "Phân tách độ chín");
  const varianceLabel = term(terms, "VARIANCE", "Chênh lệch");

  const [result, billsRes, ordersRes, readiness] = await Promise.all([
    getDashboardSummary(),
    listBills(),
    listOrders(),
    getTenantReadiness(),
  ]);

  const now = new Date();
  const greetingName = displayName ? `Xin chào, ${displayName}!` : "Xin chào!";
  const dateLine = `${weekdayVi(now)}, ${now.toLocaleDateString("vi-VN")}`;
  const year = now.getFullYear();
  const monthSeries = result.ok ? result.data.monthlySeries ?? [] : [];
  const monthNote = result.ok ? result.data.monthlySeriesNote : null;

  const vis = result.ok
    ? result.data.financialVisibility ?? {
        canViewCost: true,
        canViewRevenue: true,
        canViewMargin: true,
      }
    : { canViewCost: false, canViewRevenue: false, canViewMargin: false };

  const hasMonthAmount = monthSeries.some(
    (p) =>
      (vis.canViewCost && p.costBestAvailable != null) ||
      (vis.canViewRevenue && p.revenueBestAvailable != null)
  );

  const roll = result.ok ? result.data.baseCurrencyRollUp : null;
  const row0 = result.ok ? result.data.totalsByCurrency[0] : null;
  const currency = roll?.baseCurrency ?? row0?.currencyCode ?? "VND";
  const costAmt = Number(roll?.costBestAvailableBase ?? row0?.costBestAvailable ?? 0);
  const revAmt = Number(
    roll?.revenueBestAvailableBase ?? row0?.revenueBestAvailable ?? 0
  );
  const profitAmt = Number(
    roll?.profitBestAvailableBase ?? row0?.profitBestAvailable ?? 0
  );
  const marginPct =
    vis.canViewMargin && revAmt !== 0
      ? ((profitAmt / Math.abs(revAmt)) * 100).toFixed(1).replace(".", ",")
      : null;

  const recentBills = billsRes.ok ? billsRes.data.items.slice(0, 4) : [];
  const mat = result.ok ? result.data.maturityPipeline : null;
  const orderCount = ordersRes.ok ? ordersRes.data.length : 0;
  const billCount = result.ok ? result.data.billCount : 0;

  const taskTotal = result.ok
    ? result.data.openExceptionCount +
      result.data.pendingApprovalCount +
      result.data.overdueExceptionCount +
      result.data.openVarianceCount +
      result.data.unmatchedBankFeedCount +
      result.data.openCloseCount
    : 0;

  const notifs: { color: string; text: string; href: string }[] = [];
  if (result.ok) {
    if (result.data.openCloseCount > 0) {
      notifs.push({
        color: "#10b981",
        text: `${result.data.openCloseCount} kỳ ${closeLabel.toLowerCase()} đang mở`,
        href: "/financial-closes",
      });
    }
    if (result.data.pendingApprovalCount > 0) {
      notifs.push({
        color: "#1677e8",
        text: `Có ${result.data.pendingApprovalCount} yêu cầu chờ phê duyệt`,
        href: "/queues/approvals",
      });
    }
    if (result.data.unmatchedBankFeedCount > 0) {
      notifs.push({
        color: "#ff8500",
        text: `${result.data.unmatchedBankFeedCount} dòng ${bankFeedLabel.toLowerCase()} chưa đối soát`,
        href: "/bank-feed?status=unmatched",
      });
    }
    if (result.data.overdueExceptionCount > 0) {
      notifs.push({
        color: "#f32945",
        text: `${result.data.overdueExceptionCount} ngoại lệ quá hạn cần xử lý`,
        href: "/queues/exceptions?overdueOnly=1",
      });
    }
  }

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
          <div className="po-dash-welcome">
            <div>
              <h1>{greetingName}</h1>
              <p>
                Chào mừng bạn quay trở lại hệ thống Quản lý Chi phí Logistics
                (LCMS)
              </p>
            </div>
            <div className="po-dash-quote">
              <div className="date">{dateLine}</div>
              “Kiểm soát chi phí hôm nay, tạo lợi nhuận ngày mai”
            </div>
          </div>

          {readiness.ok && !readiness.data.readyForBill ? (
            <TenantReadinessPanel data={readiness.data} />
          ) : null}

          <div className="po-grid5">
            <Link className="po-card po-kpi" href="/orders">
              <div className="po-kicon" style={{ background: "#1677e8" }}>
                <svg viewBox="0 0 24 24">
                  <path d="M6 2.5h8l4 4V21H6z" />
                  <path d="M14 2.5V7h4M9 11h6M9 15h6M9 19h4" />
                </svg>
              </div>
              <div>
                <h4>Tổng đơn hàng</h4>
                <div className="big">{orderCount.toLocaleString("vi-VN")}</div>
                <small>Tham chiếu vận hành · LCMS</small>
              </div>
            </Link>

            {vis.canViewCost ? (
              <div className="po-card po-kpi">
                <div className="po-kicon" style={{ background: "#0aae7b" }}>
                  <svg viewBox="0 0 24 24">
                    <ellipse cx="9" cy="6" rx="5.5" ry="2.5" />
                    <path d="M3.5 6v4c0 1.4 2.5 2.5 5.5 2.5s5.5-1.1 5.5-2.5V6" />
                    <path d="M3.5 10v4c0 1.4 2.5 2.5 5.5 2.5 1 0 2-.1 2.8-.4" />
                    <ellipse cx="16.5" cy="15.5" rx="4" ry="2" />
                    <path d="M12.5 15.5v3c0 1.1 1.8 2 4 2s4-.9 4-2v-3" />
                  </svg>
                </div>
                <div>
                  <h4>Tổng {costLabel.toLowerCase()}</h4>
                  <div className="big">{formatMoney(costAmt, currency)}</div>
                  <small>{bestAvailableLabel} · {currency}</small>
                </div>
              </div>
            ) : null}

            {vis.canViewRevenue ? (
              <div className="po-card po-kpi">
                <div className="po-kicon" style={{ background: "#ff8500" }}>
                  <svg viewBox="0 0 24 24">
                    <path d="M4 20V10M10 20V5M16 20v-8M22 20V2" />
                    <path d="m3 8 6-4 6 5 6-7" />
                  </svg>
                </div>
                <div>
                  <h4>Tổng {revenueLabel.toLowerCase()}</h4>
                  <div className="big">{formatMoney(revAmt, currency)}</div>
                  <small>{bestAvailableLabel} · {currency}</small>
                </div>
              </div>
            ) : null}

            {vis.canViewMargin ? (
              <div className="po-card po-kpi">
                <div className="po-kicon" style={{ background: "#f44e65" }}>
                  <svg viewBox="0 0 24 24">
                    <circle cx="12" cy="12" r="8" />
                    <path d="M12 4v8h8" />
                  </svg>
                </div>
                <div>
                  <h4>{profitLabel}</h4>
                  <div className={`big${profitAmt < 0 ? " is-neg" : ""}`}>
                    {formatMoney(profitAmt, currency)}
                  </div>
                  <small>DT − CP ({bestAvailableLabel})</small>
                </div>
              </div>
            ) : null}

            <Link className="po-card po-kpi" href="/bills">
              <div className="po-kicon" style={{ background: "#7045d8" }}>
                <svg viewBox="0 0 24 24">
                  <path d="M6 2.5h8l4 4V21H6z" />
                  <path d="M14 2.5V7h4M9 11h6M9 15h6M9 19h4" />
                </svg>
              </div>
              <div>
                <h4>Số {billLabel}</h4>
                <div className="big">{billCount.toLocaleString("vi-VN")}</div>
                <small>Neo tài chính trong phạm vi</small>
              </div>
            </Link>
          </div>

          <section className="po-card po-section" aria-labelledby="po-tasks">
            <div className="po-section-head">
              <b id="po-tasks">
                Việc cần xử lý của bạn{" "}
                {taskTotal > 0 ? (
                  <span className="po-pill count">{taskTotal}</span>
                ) : null}
              </b>
              <Link className="po-link" href="/control">
                Xem tất cả →
              </Link>
            </div>
            <div className="po-tasks">
              <Link className="po-task" href="/queues/exceptions">
                <strong className="po-text-red">
                  {result.data.openExceptionCount}
                </strong>
                {exceptionQueueLabel}
                <small>Mã hàng đợi ngoại lệ</small>
              </Link>
              <Link className="po-task" href="/queues/approvals">
                <strong className="po-text-orange">
                  {result.data.pendingApprovalCount}
                </strong>
                Chờ phê duyệt
                <small>{approvalQueueLabel}</small>
              </Link>
              <Link
                className="po-task"
                href="/queues/exceptions?overdueOnly=1"
              >
                <strong className="po-text-red">
                  {result.data.overdueExceptionCount}
                </strong>
                Quá hạn thanh toán
                <small className="po-text-red">Xem chi tiết</small>
              </Link>
              <Link className="po-task" href="/queues/variances">
                <strong className="po-text-orange">
                  {result.data.openVarianceCount}
                </strong>
                Chênh lệch đối soát
                <small>Hàng đợi {varianceLabel.toLowerCase()}</small>
              </Link>
              <Link className="po-task" href="/bank-feed?status=unmatched">
                <strong className="po-text-red">
                  {result.data.unmatchedBankFeedCount}
                </strong>
                Sao kê chưa đối soát
                <small>Đang chờ đối soát</small>
              </Link>
              <Link className="po-task" href="/financial-closes">
                <strong className="po-text-green">
                  {result.data.openCloseCount}
                </strong>
                {closeLabel}
                <small className="po-text-green">Kỳ đang mở</small>
              </Link>
              <Link className="po-task" href="/bills">
                <strong className="po-text-blue">{billCount}</strong>
                {billLabel} cần kiểm tra
                <small className="po-text-blue">Xem danh sách</small>
              </Link>
            </div>
          </section>

          <div className="po-mid">
            <section className="po-card po-chart-card" aria-labelledby="po-chart">
              <div className="po-chart-head">
                <b id="po-chart">
                  {costLabel}, {revenueLabel.toLowerCase()} và{" "}
                  {profitLabel.toLowerCase()}
                </b>
                <span className="muted">Năm {year} · ngày hiệu lực</span>
              </div>
              <div className="po-chart-legend">
                {vis.canViewCost ? "▰ Chi phí" : null}
                {vis.canViewCost && vis.canViewRevenue ? "  " : null}
                {vis.canViewRevenue ? "▰ Doanh thu" : null}
              </div>
              {hasMonthAmount ? (
                <div className="po-chart">
                  <YearBars
                    points={monthSeries}
                    canCost={!!vis.canViewCost}
                    canRev={!!vis.canViewRevenue}
                  />
                </div>
              ) : (
                <p className="empty-state" role="status" style={{ margin: "1rem" }}>
                  {monthNote ||
                    `Chưa có số CP/DT theo tháng. Ghi dòng trên ${billLabel} rồi quay lại.`}
                </p>
              )}
              {hasMonthAmount && monthNote ? (
                <p className="muted small" style={{ margin: "0.75rem 1rem 0" }}>
                  {monthNote}
                </p>
              ) : null}
            </section>

            <section className="po-card po-best" aria-labelledby="po-ba">
              <div className="po-section-head" style={{ padding: "0 0 10px" }}>
                <b id="po-ba">
                  {bestAvailableLabel} ({currency})
                </b>
                <Link className="po-link" href="/reports">
                  Xem chi tiết →
                </Link>
              </div>
              <small className="muted">
                Theo dữ liệu: {actualLabel} → {confirmedLabel} → {expectedLabel}.
                Projection read-only.
              </small>
              <div className="po-bestgrid">
                {vis.canViewCost ? (
                  <div className="po-bestbox">
                    <b>{costLabel}</b>
                    <strong>{formatMoney(costAmt, currency)}</strong>
                    <div className="po-rows">
                      <div>
                        <span>{expectedLabel}</span>
                        <b>{mat?.costExpectedOnlyCount ?? "—"}</b>
                      </div>
                      <div>
                        <span>{confirmedLabel}</span>
                        <b>{mat?.costConfirmedOnlyCount ?? "—"}</b>
                      </div>
                      <div>
                        <span>{actualLabel}</span>
                        <b>{mat?.costActualCount ?? "—"}</b>
                      </div>
                    </div>
                  </div>
                ) : null}
                {vis.canViewRevenue ? (
                  <div className="po-bestbox">
                    <b>{revenueLabel}</b>
                    <strong>{formatMoney(revAmt, currency)}</strong>
                    <div className="po-rows">
                      <div>
                        <span>{expectedLabel}</span>
                        <b>{mat?.revenueExpectedOnlyCount ?? "—"}</b>
                      </div>
                      <div>
                        <span>{confirmedLabel}</span>
                        <b>{mat?.revenueConfirmedOnlyCount ?? "—"}</b>
                      </div>
                      <div>
                        <span>{actualLabel}</span>
                        <b>{mat?.revenueActualCount ?? "—"}</b>
                      </div>
                    </div>
                  </div>
                ) : null}
                {vis.canViewMargin ? (
                  <div className="po-bestbox">
                    <b>{profitLabel}</b>
                    <strong className={profitAmt < 0 ? "po-text-red" : undefined}>
                      {formatMoney(profitAmt, currency)}
                    </strong>
                    <div className="po-rows">
                      <div>
                        <span>Tỷ suất lợi nhuận</span>
                        <b className={profitAmt < 0 ? "po-text-red" : undefined}>
                          {marginPct != null ? `${marginPct}%` : "—"}
                        </b>
                      </div>
                      <div>
                        <span>Biên lợi nhuận</span>
                        <b>—</b>
                      </div>
                      <div>
                        <span>Số {billLabel}</span>
                        <b>{billCount}</b>
                      </div>
                    </div>
                  </div>
                ) : null}
              </div>
            </section>
          </div>

          <div className="po-lower">
            <section className="po-card po-maturity" aria-labelledby="po-mat">
              <div className="po-section-head" style={{ padding: 0 }}>
                <div>
                  <b id="po-mat">{maturityLabel}</b>
                  <br />
                  <small className="muted">
                    Số dòng {costLabel}/{revenueLabel} theo lớp độ chín – không
                    phải số tiền.
                  </small>
                </div>
              </div>
              {mat && (vis.canViewCost || vis.canViewRevenue) ? (
                <div className="po-maturity-grid">
                  {vis.canViewCost ? (
                    <div className="po-mblock">
                      <b>{costLabel}</b>
                      <div className="po-mcols">
                        <div className="po-mcol">
                          <strong>{mat.costExpectedOnlyCount}</strong>
                          {expectedLabel}
                          <br />
                          <small>Chờ xử lý</small>
                        </div>
                        <div className="po-mcol">
                          <strong>{mat.costConfirmedOnlyCount}</strong>
                          {confirmedLabel}
                          <br />
                          <small>Chưa thực tế</small>
                        </div>
                        <div className="po-mcol">
                          <strong>{mat.costActualCount}</strong>
                          {actualLabel}
                          <br />
                          <small className="po-text-blue">Đã có thực tế</small>
                        </div>
                      </div>
                    </div>
                  ) : null}
                  {vis.canViewRevenue ? (
                    <div className="po-mblock">
                      <b>{revenueLabel}</b>
                      <div className="po-mcols">
                        <div className="po-mcol">
                          <strong>{mat.revenueExpectedOnlyCount}</strong>
                          {expectedLabel}
                        </div>
                        <div className="po-mcol">
                          <strong>{mat.revenueConfirmedOnlyCount}</strong>
                          {confirmedLabel}
                        </div>
                        <div className="po-mcol">
                          <strong>{mat.revenueActualCount}</strong>
                          {actualLabel}
                          <br />
                          <small className="po-text-green">Đã có thực tế</small>
                        </div>
                      </div>
                    </div>
                  ) : null}
                </div>
              ) : (
                <p className="empty-state" role="status">
                  Chưa có dữ liệu độ chín.
                </p>
              )}
            </section>

            <section className="po-card po-tablecard" aria-labelledby="po-recent">
              <div className="po-section-head" style={{ padding: "0 0 8px" }}>
                <b id="po-recent">
                  Đơn hàng / {billLabel} gần đây
                </b>
                <Link className="po-link" href="/bills">
                  Xem tất cả →
                </Link>
              </div>
              {recentBills.length === 0 ? (
                <p className="empty-state" role="status">
                  Chưa có {billLabel}.{" "}
                  <Link className="po-link" href="/bills/new">
                    Tạo mới
                  </Link>
                </p>
              ) : (
                <table className="po-tbl">
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
                          <Link href={`/bills/${b.id}`}>{b.billNo}</Link>
                        </td>
                        <td>{billTypeLabel(b.billType)}</td>
                        <td>
                          {new Date(b.createdAt).toLocaleDateString("vi-VN")}
                        </td>
                        <td>
                          <span className={statusPillClass(b.operationalStatus)}>
                            {operationalStatusLabel(b.operationalStatus)}
                          </span>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </section>
          </div>

          <div className="po-bottom">
            <section className="po-card po-mini" aria-labelledby="po-docs">
              <div className="po-section-head" style={{ padding: "0 0 8px" }}>
                <b id="po-docs">{docLabel}</b>
                <Link className="po-link" href="/documents">
                  Xem chi tiết →
                </Link>
              </div>
              {result.data.documents ? (
                <div className="po-mini3">
                  <div className="po-minibox">
                    <strong className="po-text-blue">
                      {result.data.documents.awaitingAcceptanceCount}
                    </strong>
                    Đã nhận,
                    <br />
                    chờ hạch toán
                  </div>
                  <div className="po-minibox">
                    <strong className="po-text-orange">
                      {result.data.documents.acceptedUnmatchedCount}
                    </strong>
                    Đã chấp nhận,
                    <br />
                    chưa khớp đủ
                  </div>
                  <div className="po-minibox">
                    <strong className="po-text-red">
                      {result.data.documents.draftMatchCount}
                    </strong>
                    Khớp nháp /
                    <br />
                    chờ xử lý
                  </div>
                </div>
              ) : (
                <p className="muted">Không có quyền xem cụm chứng từ.</p>
              )}
            </section>

            <section className="po-card po-mini" aria-labelledby="po-apar">
              <div className="po-section-head" style={{ padding: "0 0 8px" }}>
                <b id="po-apar">
                  Khoản phải trả / Khoản phải thu
                </b>
                <Link className="po-link" href="/ap-ar">
                  Xem chi tiết →
                </Link>
              </div>
              {result.data.apAr ? (
                <div className="po-bestgrid">
                  <div className="po-bestbox">
                    <b>{apLabel} (AP)</b>
                    <div className="po-mini3" style={{ marginTop: 8 }}>
                      <div>
                        <strong>
                          {result.data.apAr.openAccountsPayableCount}
                        </strong>
                        <small>Chưa tất toán hết</small>
                      </div>
                      <div>
                        <strong>
                          {result.data.apAr.openPayableExposureCount}
                        </strong>
                        <small>Exposure mở</small>
                      </div>
                    </div>
                  </div>
                  <div className="po-bestbox" style={{ gridColumn: "span 2" }}>
                    <b className="po-text-green">{arLabel} (AR)</b>
                    <div className="po-mini3" style={{ marginTop: 8 }}>
                      <div>
                        <strong className="po-text-green">
                          {result.data.apAr.openAccountsReceivableCount}
                        </strong>
                        <small>Chưa ghi nhận đủ</small>
                      </div>
                      <div>
                        <strong className="po-text-green">
                          {result.data.apAr.openReceivableExposureCount}
                        </strong>
                        <small>Exposure mở</small>
                      </div>
                    </div>
                  </div>
                </div>
              ) : (
                <p className="muted">Không có quyền xem AP/AR.</p>
              )}
            </section>

            <section className="po-card po-mini" aria-labelledby="po-notif">
              <div className="po-section-head" style={{ padding: 0 }}>
                <b id="po-notif">Thông báo hệ thống</b>
                <Link className="po-link" href="/control">
                  Xem tất cả →
                </Link>
              </div>
              {notifs.length === 0 ? (
                <div className="po-notice">
                  <span className="po-dot" />
                  Không có cảnh báo từ hàng đợi hiện tại.
                </div>
              ) : (
                notifs.map((n) => (
                  <div className="po-notice" key={n.href + n.text}>
                    <span
                      className="po-dot"
                      style={{ background: n.color }}
                    />
                    <Link href={n.href}>{n.text}</Link>
                  </div>
                ))
              )}
            </section>
          </div>

          <p className="po-dash-note">{result.data.note}</p>
        </div>
      )}
    </AppShell>
  );
}
