"use client";

import { ApplyRatingFinancialsButton } from "@/components/ApplyRatingFinancialsButton";
import {
  BillRatingFinancialWorkspace,
  type BillEffectiveFinancialWorkspaceData,
} from "@/components/BillRatingFinancialWorkspace";
import { formatMoney } from "@/lib/money";

const BILL_ID = "7480847E-2F80-4435-A097-318812EC8FE0";

const workspace: BillEffectiveFinancialWorkspaceData = {
  billId: BILL_ID,
  billNo: "LSSTEST02112026002",
  effectiveCost: 1740000,
  effectiveRevenue: 2440000,
  bestAvailableProfit: 700000,
  lines: [
    {
      id: "c-freight-act",
      side: "cost",
      chargeCode: "FREIGHT",
      economicIdentityKey: "…|BUY|FREIGHT|bill:7480847e…",
      sourceLabel: "Rating Run",
      maturityLabel: "Thực tế",
      amount: 1440000,
      currencyCode: "VND",
      effectiveIncluded: true,
      participationBadge: "Có (Included)",
      exclusionReasonVi: null,
    },
    {
      id: "c-freight-exp",
      side: "cost",
      chargeCode: "FREIGHT",
      economicIdentityKey: "…|BUY|FREIGHT|bill:7480847e…",
      sourceLabel: "Rating Run",
      maturityLabel: "Dự kiến",
      amount: 1440000,
      currencyCode: "VND",
      effectiveIncluded: false,
      participationBadge: "Không (Excluded)",
      exclusionReasonVi: "Đã có số liệu Thực tế thay thế",
    },
    {
      id: "c-dg-act",
      side: "cost",
      chargeCode: "DONGGOI_SGN_HAN_MUA",
      economicIdentityKey: "…|BUY|DONGGOI|…",
      sourceLabel: "Rating Run",
      maturityLabel: "Dự kiến",
      amount: 300000,
      currencyCode: "VND",
      effectiveIncluded: true,
      participationBadge: "Có (Included)",
      exclusionReasonVi: null,
    },
    {
      id: "c-dg-old",
      side: "cost",
      chargeCode: "DONGGOI_SGN_HAN_MUA",
      economicIdentityKey: "…|BUY|DONGGOI|…",
      sourceLabel: "Rating Run",
      maturityLabel: "Dự kiến",
      amount: 300000,
      currencyCode: "VND",
      effectiveIncluded: false,
      participationBadge: "Không (Excluded)",
      exclusionReasonVi: "Bản dự kiến cũ đã được thay thế (Superseded)",
    },
    {
      id: "r-freight",
      side: "revenue",
      chargeCode: "FREIGHT",
      economicIdentityKey: "…|SELL|FREIGHT|…",
      sourceLabel: "Rating Run",
      maturityLabel: "Thực tế",
      amount: 2040000,
      currencyCode: "VND",
      effectiveIncluded: true,
      participationBadge: "Có (Included)",
      exclusionReasonVi: null,
    },
    {
      id: "r-dg",
      side: "revenue",
      chargeCode: "DONGGOI_SGN_HAN",
      economicIdentityKey: "…|SELL|DONGGOI|…",
      sourceLabel: "Manual",
      maturityLabel: "Dự kiến",
      amount: 400000,
      currencyCode: "VND",
      effectiveIncluded: true,
      participationBadge: "Có (Included)",
      exclusionReasonVi: null,
    },
  ],
  profitKpis: [
    {
      code: "expected",
      labelVi: "Lợi nhuận Dự kiến (Expected)",
      amount: 700000,
      currencyCode: "VND",
      incomplete: false,
      maturityMix: false,
    },
    {
      code: "best_available",
      labelVi: "Lợi nhuận Ước tính tốt nhất (Best Available)",
      amount: 700000,
      currencyCode: "VND",
      incomplete: false,
      maturityMix: true,
      warningVi: "Có pha trộn giữa dòng Thực tế và dòng Dự kiến (Maturity Mix).",
    },
    {
      code: "actual_only",
      labelVi: "Lợi nhuận Thực tế (Actual-only)",
      amount: 600000,
      currencyCode: "VND",
      incomplete: true,
      maturityMix: false,
      warningVi: "Chưa đủ dữ liệu thực tế (Incomplete)",
    },
  ],
  timeline: [
    {
      at: "2026-11-03T02:12:00Z",
      eventType: "rating",
      labelVi: "Tính thử Rating BUY",
      detail: "RateVersion v3 · ops.user",
    },
    {
      at: "2026-11-03T02:15:00Z",
      eventType: "apply",
      labelVi: "Áp dụng vào hồ sơ tài chính",
      detail: "actor: ops.user",
    },
    {
      at: "2026-11-03T02:16:00Z",
      eventType: "supersede",
      labelVi: "Supersede Expected DONGGOI",
      detail: "duplicate cleanup",
    },
  ],
};

export function RatingDedupWorkspacePreview() {
  return (
    <div className="stack" id="bill-rating" style={{ padding: "1.25rem 1.5rem 2rem" }}>
      <div id="shot-01" className="stack">
        <h2 className="section-title">Tính giá &amp; Tài chính</h2>
        <p className="note">
          Bill <strong>LSSTEST02112026002</strong> · EffectiveFactSelector · Cost{" "}
          {formatMoney(1740000, "VND")} · Revenue {formatMoney(2440000, "VND")} · Best Available Profit{" "}
          {formatMoney(700000, "VND")}
        </p>
        <div
          className="stat-card-grid"
          style={{ display: "grid", gridTemplateColumns: "repeat(3,minmax(0,1fr))", gap: "0.75rem" }}
        >
          {workspace.profitKpis.map((k) => (
            <div
              key={k.code}
              className="stat-card"
              style={{ border: "1px solid #e2e8f0", borderRadius: 8, padding: "12px 14px", background: "#fff" }}
            >
              <div className="small muted">{k.labelVi}</div>
              <div style={{ fontSize: "1.25rem", fontWeight: 700 }}>
                {k.amount == null ? "—" : formatMoney(k.amount, k.currencyCode)}
              </div>
              {k.maturityMix || k.incomplete ? (
                <div className="alert alert-warning" role="status" style={{ marginTop: 8 }}>
                  {k.warningVi}
                </div>
              ) : null}
            </div>
          ))}
        </div>
      </div>

      <div id="shot-02" className="stack" style={{ marginTop: "1.5rem" }}>
        <h3 className="section-title sm">A. Ngữ cảnh tính giá</h3>
        <div
          className="panel"
          style={{
            display: "grid",
            gridTemplateColumns: "repeat(auto-fit,minmax(140px,1fr))",
            gap: "0.75rem",
            padding: "12px 14px",
            border: "1px solid #e2e8f0",
            borderRadius: 8,
            background: "#f8fafc",
          }}
        >
          <div>
            <span className="muted small block">Customer / Vendor</span>
            <strong>LSS Test Customer · Vendor A</strong>
          </div>
          <div>
            <span className="muted small block">Bill / Leg / Route</span>
            <strong>LSSTEST02112026002 · Leg 1 · SGN→HAN</strong>
          </div>
          <div>
            <span className="muted small block">Service / Mode / CW</span>
            <strong>FTL · Road · CW 1.200 kg</strong>
          </div>
          <div>
            <span className="muted small block">as-of · Chiều</span>
            <strong>03/11/2026 · MUA (BUY) / BÁN (SELL)</strong>
          </div>
        </div>
        <h3 className="section-title sm">B. Phương án &amp; So sánh</h3>
        <p className="note">
          Nhãn trạng thái Rating: <em>Tính thử (Chưa áp dụng)</em>, <em>Có thể áp dụng</em>, <em>Đang áp dụng</em>,{" "}
          <em>Chỉ so sánh</em>, <em>Đã thay thế (Lịch sử)</em> — không dùng «Đã xác nhận» cho Rating.
        </p>
        <div className="table-wrap">
          <table className="data-table">
            <thead>
              <tr>
                <th>Chiều</th>
                <th>Phương án đang áp dụng (Current Applied)</th>
                <th>Phương án tính thử (Candidate)</th>
                <th>Trạng thái Rating</th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td>MUA (BUY)</td>
                <td>{formatMoney(1740000, "VND")}</td>
                <td>{formatMoney(1740000, "VND")}</td>
                <td>
                  <span className="badge ok">Đang áp dụng</span>
                </td>
              </tr>
              <tr>
                <td>BÁN (SELL)</td>
                <td>{formatMoney(2440000, "VND")}</td>
                <td>{formatMoney(2440000, "VND")}</td>
                <td>
                  <span className="badge ok">Có thể áp dụng</span>
                </td>
              </tr>
              <tr>
                <td>So sánh</td>
                <td>—</td>
                <td>{formatMoney(1850000, "VND")}</td>
                <td>
                  <span className="badge warn">Tính thử / Chỉ so sánh</span>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <div id="shot-03" className="stack" style={{ marginTop: "1.5rem" }}>
        <h3 className="section-title sm">C. Xem trước &amp; Áp dụng</h3>
        <div className="table-wrap">
          <table className="data-table">
            <thead>
              <tr>
                <th>Khoản</th>
                <th>Hành động</th>
                <th>Số tiền</th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td>FREIGHT</td>
                <td>Giữ Actual · cập nhật ExpectedAmount</td>
                <td>{formatMoney(1440000, "VND")}</td>
              </tr>
              <tr>
                <td>DONGGOI</td>
                <td>Thay thế Expected cũ (Supersede)</td>
                <td>{formatMoney(300000, "VND")}</td>
              </tr>
            </tbody>
          </table>
        </div>
        <p>
          <strong>Δ Cost 0 · Δ Revenue 0 · Δ Profit 0</strong>
        </p>
        <div className="alert alert-warning" role="alert">
          <strong>Cảnh báo:</strong> Nghĩa vụ FREIGHT đã có Thực tế (Actual) — Apply không ghi đè Actual/Confirmed.
        </div>
        <ApplyRatingFinancialsButton ratingId="00000000-0000-0000-0000-000000000001" directionLabel="MUA (BUY)" />
      </div>

      <div id="shot-workspace">
        <BillRatingFinancialWorkspace billId={BILL_ID} initialData={workspace} showMaturityDialogOpen />
      </div>
    </div>
  );
}
