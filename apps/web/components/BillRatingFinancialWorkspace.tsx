"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { ApplyRatingFinancialsButton } from "@/components/ApplyRatingFinancialsButton";
import { formatDateTimeVi, formatMoney } from "@/lib/money";

type WorkspaceLine = {
  id: string;
  side: string;
  chargeCode: string;
  economicIdentityKey: string;
  sourceLabel: string;
  maturityLabel: string;
  amount: number;
  currencyCode: string;
  effectiveIncluded: boolean;
  participationBadge: string;
  exclusionReasonVi?: string | null;
};

type Kpi = {
  code: string;
  labelVi: string;
  amount: number | null;
  currencyCode: string;
  incomplete: boolean;
  maturityMix: boolean;
  warningVi?: string | null;
};

type TimelineEvent = {
  at: string;
  eventType: string;
  labelVi: string;
  detail?: string | null;
  ratingId?: string | null;
};

type Workspace = {
  billId: string;
  billNo: string;
  lines: WorkspaceLine[];
  profitKpis: Kpi[];
  timeline: TimelineEvent[];
  effectiveCost: number;
  effectiveRevenue: number;
  bestAvailableProfit: number;
};

export type BillEffectiveFinancialWorkspaceData = Workspace;

type Props = {
  billId: string;
  buyRatingId?: string | null;
  sellRatingId?: string | null;
  /** When set, skip BFF fetch (DEV preview / screenshot). */
  initialData?: Workspace | null;
  showMaturityDialogOpen?: boolean;
};

export function BillRatingFinancialWorkspace({
  billId,
  buyRatingId,
  sellRatingId,
  initialData = null,
  showMaturityDialogOpen = false,
}: Props) {
  const [data, setData] = useState<Workspace | null>(initialData);
  const [error, setError] = useState<string | null>(null);
  const [maturityBusy, setMaturityBusy] = useState<string | null>(null);
  const [dialogOpen, setDialogOpen] = useState(showMaturityDialogOpen);

  const load = useCallback(async () => {
    if (initialData) {
      setData(initialData);
      return;
    }
    setError(null);
    const res = await fetch(`/bff/bills/${billId}/effective-financial-workspace`, {
      headers: { Accept: "application/json" },
    });
    if (!res.ok) {
      const payload = (await res.json().catch(() => ({}))) as { message?: string };
      setError(payload.message || "Không tải được hồ sơ tài chính hợp nhất.");
      return;
    }
    setData((await res.json()) as Workspace);
  }, [billId, initialData]);

  useEffect(() => {
    void load();
  }, [load]);

  async function recordMaturity(side: string, id: string, kind: "confirm" | "actualize") {
    const evidence = window.prompt(
      kind === "confirm"
        ? "Nhập bằng chứng xác nhận (bắt buộc):"
        : "Nhập bằng chứng ghi nhận Thực tế (bắt buộc):"
    );
    if (!evidence?.trim()) return;
    setMaturityBusy(id);
    try {
      const path =
        side === "revenue"
          ? `/bff/revenues/${id}/${kind === "confirm" ? "confirm" : "actualize"}`
          : `/bff/costs/${id}/${kind === "confirm" ? "confirm" : "actualize"}`;
      const res = await fetch(path, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Accept: "application/json",
          "Idempotency-Key": crypto.randomUUID(),
        },
        body: JSON.stringify({ evidence: evidence.trim() }),
      });
      if (!res.ok) {
        const payload = (await res.json().catch(() => ({}))) as { message?: string };
        setError(payload.message || "Không cập nhật được mức độ trưởng thành.");
        return;
      }
      await load();
    } finally {
      setMaturityBusy(null);
    }
  }

  return (
    <div className="stack" style={{ marginTop: "1.25rem" }}>
      {error ? (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      ) : null}
      {!data ? (
        <p className="muted">Đang tải hồ sơ hiệu lực…</p>
      ) : (
        <>
          <div id="workspace-area-d" className="stack">
            <h3 className="section-title sm">D. Hồ sơ tài chính hợp nhất</h3>
            <p className="note">
              Cột «Tham gia tổng» phản ánh EffectiveFactSelector. Drill-down:{" "}
              <Link className="row-link" href={`/bills/${billId}?tab=costs`}>
                Chi phí
              </Link>
              {" · "}
              <Link className="row-link" href={`/bills/${billId}?tab=revenues`}>
                Doanh thu
              </Link>
            </p>
            <div className="table-wrap">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Khoản phí</th>
                    <th>Định danh nghĩa vụ</th>
                    <th>Nguồn</th>
                    <th>Mức độ trưởng thành</th>
                    <th className="num">Số tiền</th>
                    <th>Tham gia tổng</th>
                    <th>Lý do không tham gia</th>
                    <th>Thao tác</th>
                  </tr>
                </thead>
                <tbody>
                  {data.lines.map((l) => (
                    <tr key={`${l.side}-${l.id}`}>
                      <td>
                        {l.side === "cost" ? "Chi phí" : "Doanh thu"} · {l.chargeCode}
                      </td>
                      <td className="small muted">{l.economicIdentityKey.slice(0, 24)}…</td>
                      <td>{l.sourceLabel}</td>
                      <td>{l.maturityLabel}</td>
                      <td className="num">{formatMoney(l.amount, l.currencyCode)}</td>
                      <td>
                        <span className={l.effectiveIncluded ? "badge ok" : "badge warn"}>
                          {l.participationBadge}
                        </span>
                      </td>
                      <td className="small">{l.exclusionReasonVi || "—"}</td>
                      <td>
                        {l.effectiveIncluded && l.maturityLabel === "Dự kiến" ? (
                          <div className="row-actions">
                            <button
                              type="button"
                              className="btn btn-sm btn-ghost"
                              disabled={maturityBusy === l.id}
                              onClick={() => void recordMaturity(l.side, l.id, "confirm")}
                            >
                              Xác nhận
                            </button>
                            <button
                              type="button"
                              className="btn btn-sm"
                              disabled={maturityBusy === l.id}
                              onClick={() => void recordMaturity(l.side, l.id, "actualize")}
                            >
                              Ghi nhận Thực tế
                            </button>
                          </div>
                        ) : (
                          <span className="muted">—</span>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>

          <div id="workspace-area-e" className="stack" style={{ marginTop: "1.25rem" }}>
            <h3 className="section-title sm">E. Lợi nhuận phân tầng</h3>
            <div
              className="stat-card-grid"
              style={{ display: "grid", gridTemplateColumns: "repeat(3,minmax(0,1fr))", gap: "0.75rem" }}
            >
              {data.profitKpis.map((k) => (
                <div
                  key={k.code}
                  className="stat-card"
                  style={{ border: "1px solid #e2e8f0", borderRadius: 8, padding: "12px 14px", background: "#fff" }}
                >
                  <div className="small muted">{k.labelVi}</div>
                  <div style={{ fontSize: "1.25rem", fontWeight: 700 }}>
                    {k.amount == null ? "—" : formatMoney(k.amount, k.currencyCode)}
                  </div>
                  {k.maturityMix ? (
                    <div className="alert alert-warning" role="status" style={{ marginTop: 8 }}>
                      {k.warningVi}
                    </div>
                  ) : null}
                  {k.incomplete ? (
                    <div className="alert alert-warning" role="status" style={{ marginTop: 8 }}>
                      {k.warningVi || "Chưa đủ dữ liệu thực tế (Incomplete)"}
                    </div>
                  ) : null}
                </div>
              ))}
            </div>
            <p className="note">
              Best Available Cost {formatMoney(data.effectiveCost, "VND")} · Revenue{" "}
              {formatMoney(data.effectiveRevenue, "VND")} · Profit {formatMoney(data.bestAvailableProfit, "VND")}
            </p>
          </div>

          {(buyRatingId || sellRatingId) && (
            <>
              <h3 className="section-title sm">C. Xem trước &amp; Áp dụng</h3>
              <div style={{ display: "flex", flexWrap: "wrap", gap: "1rem" }}>
                {buyRatingId ? (
                  <div>
                    <div className="small muted">MUA (BUY)</div>
                    <ApplyRatingFinancialsButton ratingId={buyRatingId} directionLabel="MUA (BUY)" />
                  </div>
                ) : null}
                {sellRatingId ? (
                  <div>
                    <div className="small muted">BÁN (SELL)</div>
                    <ApplyRatingFinancialsButton ratingId={sellRatingId} directionLabel="BÁN (SELL)" />
                  </div>
                ) : null}
              </div>
            </>
          )}

          <div id="workspace-area-f" className="stack" style={{ marginTop: "1.25rem" }}>
            <h3 className="section-title sm">F. Timeline lịch sử</h3>
            <ol className="stack" style={{ paddingLeft: "1.2rem" }}>
              {data.timeline.slice(0, 12).map((t, i) => (
                <li key={`${t.at}-${i}`}>
                  <strong>{t.labelVi}</strong> · {formatDateTimeVi(t.at)}
                  {t.detail ? <span className="muted small"> — {t.detail}</span> : null}
                </li>
              ))}
            </ol>
            {dialogOpen ? (
              <div
                className="panel"
                role="dialog"
                aria-modal="true"
                aria-label="Ghi nhận Thực tế"
                style={{
                  marginTop: "1rem",
                  maxWidth: 420,
                  padding: "16px",
                  border: "1px solid #e2e8f0",
                  borderRadius: 10,
                  boxShadow: "0 8px 24px rgba(15,23,42,.12)",
                }}
              >
                <h4 style={{ marginTop: 0 }}>Ghi nhận Thực tế</h4>
                <p className="note">
                  Bằng chứng (Evidence) bắt buộc. Hệ thống gọi API domain, không tạo dòng Cost/Revenue mới.
                </p>
                <label className="muted small" htmlFor="uat-evidence">
                  Evidence
                </label>
                <textarea
                  id="uat-evidence"
                  className="input"
                  rows={3}
                  defaultValue="Hóa đơn nhà cung cấp #INV-2026-1103"
                  style={{ width: "100%", marginTop: 6 }}
                />
                <div style={{ display: "flex", gap: 8, marginTop: 12 }}>
                  <button type="button" className="btn btn-sm">
                    Lưu Thực tế
                  </button>
                  <button type="button" className="btn btn-sm btn-ghost" onClick={() => setDialogOpen(false)}>
                    Hủy
                  </button>
                </div>
              </div>
            ) : null}
          </div>
        </>
      )}
    </div>
  );
}
