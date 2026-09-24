import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { AuditTrailPanel } from "@/components/AuditTrailPanel";
import { CloseSnapshotButton } from "@/components/CloseSnapshotButton";
import { ReopenCloseButton } from "@/components/ReopenCloseButton";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import {
  canReopen,
  canSnapshot,
  closeStatusLabel,
  getFinancialClose,
  getFinancialCloseEligibility,
  getFinancialClosePnl,
  scopeTypeLabel,
} from "@/lib/financial-closes";
import { formatDateTimeVi, formatMoney } from "@/lib/money";

type Params = Promise<{ id: string }>;

export default async function FinancialCloseDetailPage({
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
  const closeLabel = term(terms, "FINANCIAL_CLOSE", "Chốt tài chính");
  const snapshotLabel = term(
    terms,
    "FINANCIAL_CLOSE_SNAPSHOT",
    "Bản chốt tài chính"
  );
  const pnlLabel = term(terms, "CLOSE_PNL", "P&L chốt");
  const billLabel = term(terms, "BILL", "Bill");
  const periodLock = term(terms, "PERIOD_LOCK", "Khóa kỳ");

  const closeRes = await getFinancialClose(id);

  if (!closeRes.ok) {
    return (
      <AppShell terms={terms} active="financial-closes">
        <section className="panel panel-wide">
          <div className="alert alert-error" role="alert">
            {closeRes.message}
          </div>
          <Link className="btn btn-ghost" href="/financial-closes">
            Quay lại
          </Link>
        </section>
      </AppShell>
    );
  }

  const close = closeRes.data;
  const hasSnapshot = (close.snapshots?.length ?? 0) > 0;
  const pnlRes = hasSnapshot ? await getFinancialClosePnl(id) : null;
  const eligibilityRes = canSnapshot(close.status)
    ? await getFinancialCloseEligibility(id)
    : null;
  const latestSnapshot = hasSnapshot
    ? [...close.snapshots].sort(
        (a, b) => b.snapshotVersion - a.snapshotVersion
      )[0]
    : null;

  return (
    <AppShell terms={terms} active="financial-closes">
      <section className="panel panel-wide">
        <p className="breadcrumb">
          <Link href="/financial-closes">{closeLabel}</Link>
          {" / "}
          Chi tiết
        </p>
        <h1>
          {closeLabel} · {scopeTypeLabel(terms, close.scopeType)} v
          {close.versionNo}
        </h1>
        <p className="lede">
          Hành động chính: tạo {snapshotLabel.toLowerCase()} khi đang mở / mở
          lại. Snapshot bất biến; có thể kích hoạt {periodLock.toLowerCase()}.
        </p>

        <dl className="metric-grid">
          <div>
            <dt>Trạng thái</dt>
            <dd>{closeStatusLabel(terms, close.status)}</dd>
          </div>
          <div>
            <dt>Chính sách</dt>
            <dd>{close.policyVersion}</dd>
          </div>
          <div>
            <dt>Tiền tệ gốc</dt>
            <dd>{close.baseCurrency}</dd>
          </div>
          <div>
            <dt>Phạm vi</dt>
            <dd>
              {close.scopeType === "bill" && close.scopeId ? (
                <Link className="row-link" href={`/bills/${close.scopeId}`}>
                  {billLabel} {close.scopeId.slice(0, 8)}…
                </Link>
              ) : close.periodFrom || close.periodTo ? (
                `${close.periodFrom ?? "…"} → ${close.periodTo ?? "…"}`
              ) : (
                scopeTypeLabel(terms, close.scopeType)
              )}
            </dd>
          </div>
          <div>
            <dt>Bắt đầu</dt>
            <dd>
              {close.startedAt ? formatDateTimeVi(close.startedAt) : "—"}
            </dd>
          </div>
          <div>
            <dt>Khóa</dt>
            <dd>
              {close.lockedAt ? formatDateTimeVi(close.lockedAt) : "—"}
            </dd>
          </div>
        </dl>

        {close.notes ? <p className="note">Ghi chú: {close.notes}</p> : null}
        {close.reopenReason ? (
          <p className="note">Lý do mở lại: {close.reopenReason}</p>
        ) : null}

        {eligibilityRes ? (
          <>
            <h2 className="section-title">Điều kiện chốt</h2>
            {!eligibilityRes.ok ? (
              <div className="alert alert-error" role="alert">
                {eligibilityRes.message}
              </div>
            ) : (
              <>
                <p className="note">
                  {eligibilityRes.data.eligible
                    ? `Đủ điều kiện tạo bản chốt (${eligibilityRes.data.gates.filter((g) => g.passed).length}/${eligibilityRes.data.gates.length} điều kiện đạt).`
                    : `Còn điều kiện chặn — ${eligibilityRes.data.gates.filter((g) => g.passed).length}/${eligibilityRes.data.gates.length} điều kiện đạt. Xử lý xong rồi tạo bản chốt.`}
                </p>
                <ul className="close-gate-list" aria-label="Checklist điều kiện chốt">
                  {eligibilityRes.data.gates.map((g) => (
                    <li
                      key={g.code}
                      className={
                        g.passed ? "close-gate-item pass" : "close-gate-item fail"
                      }
                    >
                      <span
                        className={
                          g.passed
                            ? "status-pill status-completed"
                            : "status-pill status-blocked"
                        }
                      >
                        {g.passed ? "Đạt" : "Chặn"}
                      </span>
                      <div className="close-gate-body">
                        <strong>{g.label}</strong>
                        {!g.passed && g.failReason ? (
                          <span className="muted small block">{g.failReason}</span>
                        ) : null}
                      </div>
                    </li>
                  ))}
                </ul>
              </>
            )}
          </>
        ) : null}

        <div className="cta-row">
          <CloseSnapshotButton
            terms={terms}
            closeId={close.id}
            rowVersion={close.rowVersion}
            canRun={canSnapshot(close.status)}
            eligible={
              eligibilityRes?.ok === true && eligibilityRes.data.eligible
            }
            blockedHint={
              eligibilityRes?.ok === true && !eligibilityRes.data.eligible
                ? `Còn ${eligibilityRes.data.gates.filter((g) => !g.passed).length} điều kiện chặn — xử lý checklist bên trên rồi mới tạo bản chốt.`
                : eligibilityRes && !eligibilityRes.ok
                  ? "Không tải được điều kiện chốt — không cho tạo bản chốt."
                  : null
            }
          />
          <ReopenCloseButton
            terms={terms}
            closeId={close.id}
            canRun={canReopen(close.status)}
            rowVersion={close.rowVersion}
          />
        </div>

        <h2 className="section-title">{snapshotLabel}</h2>
        {!hasSnapshot ? (
          <div className="empty-state" role="status">
            Chưa có {snapshotLabel.toLowerCase()}. Tạo bản chốt để khóa và xem
            P&amp;L.
          </div>
        ) : (
          <div className="table-wrap">
            <table className="data-table">
              <thead>
                <tr>
                  <th scope="col">Phiên bản</th>
                  <th scope="col">Thời điểm</th>
                  <th scope="col">Hash</th>
                  <th scope="col">Chi tiết</th>
                </tr>
              </thead>
              <tbody>
                {close.snapshots.map((s) => (
                  <tr key={s.id}>
                    <td>v{s.snapshotVersion}</td>
                    <td>{formatDateTimeVi(s.closedAt)}</td>
                    <td>
                      <code className="mono-id">
                        {s.immutableHash.slice(0, 12)}…
                      </code>
                    </td>
                    <td>{s.details?.length ?? 0} dòng</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {latestSnapshot?.details?.some((d) => d.metricKey === "waiver") ? (
          <>
            <h2 className="section-title">Miễn đã ghi vào snapshot</h2>
            <div className="table-wrap">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Ghi chú</th>
                    <th>Nguồn</th>
                  </tr>
                </thead>
                <tbody>
                  {latestSnapshot.details
                    .filter((d) => d.metricKey === "waiver")
                    .map((d) => (
                      <tr key={d.id}>
                        <td>{d.notes || "—"}</td>
                        <td>
                          {d.sourceId ? (
                            <Link href={`/queues/exceptions`}>Ngoại lệ</Link>
                          ) : (
                            "—"
                          )}
                        </td>
                      </tr>
                    ))}
                </tbody>
              </table>
            </div>
          </>
        ) : null}

        <h2 className="section-title">{pnlLabel}</h2>
        {!hasSnapshot ? (
          <p className="note">Cần snapshot trước khi xem P&amp;L.</p>
        ) : pnlRes && !pnlRes.ok ? (
          <div className="alert alert-error" role="alert">
            {pnlRes.message}
          </div>
        ) : pnlRes?.ok ? (
          <>
            <p className="note">{pnlRes.data.note}</p>
            <dl className="metric-grid">
              <div>
                <dt>Doanh thu</dt>
                <dd>
                  {formatMoney(
                    pnlRes.data.revenueTotal,
                    pnlRes.data.baseCurrency
                  )}
                </dd>
              </div>
              <div>
                <dt>Chi phí</dt>
                <dd>
                  {formatMoney(
                    pnlRes.data.costTotal,
                    pnlRes.data.baseCurrency
                  )}
                </dd>
              </div>
              <div>
                <dt>Lợi nhuận</dt>
                <dd>
                  {formatMoney(
                    pnlRes.data.profitTotal,
                    pnlRes.data.baseCurrency
                  )}
                </dd>
              </div>
              <div>
                <dt>AP còn dư</dt>
                <dd>
                  {formatMoney(
                    pnlRes.data.apOutstandingTotal,
                    pnlRes.data.baseCurrency
                  )}
                </dd>
              </div>
              <div>
                <dt>AR còn dư</dt>
                <dd>
                  {formatMoney(
                    pnlRes.data.arOutstandingTotal,
                    pnlRes.data.baseCurrency
                  )}
                </dd>
              </div>
            </dl>
          </>
        ) : null}

        {latestSnapshot ? (
          <AuditTrailPanel
            terms={terms}
            objectType="financial_close_snapshot"
            objectId={latestSnapshot.id}
            title={`${term(terms, "AUDIT_TRAIL", "Nhật ký kiểm toán")} · ${snapshotLabel} v${latestSnapshot.snapshotVersion}`}
          />
        ) : null}
      </section>
    </AppShell>
  );
}
