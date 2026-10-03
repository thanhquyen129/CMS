"use client";
/**
 * BillCashFlowMap v2 — node-per-line layout, pure SVG, 0 external deps.
 *
 * Layout
 * ──────
 *   LEFT column   : one node per Revenue line (dòng tiền vào — AR)
 *   CENTRE node   : Bill summary (tổng DT / CP / LN / biên)
 *   RIGHT column  : one node per Cost line   (dòng tiền ra — AP)
 *
 * Each node has a curved bezier arrow to/from the centre Bill node.
 * Maturity shown as a coloured pill (Dự kiến / Xác nhận / Thực tế).
 * Falls back to bucket-level if no line items are provided.
 */

import { formatMoney } from "@/lib/money";
import type { CostListItem, RevenueListItem } from "@/lib/costs-revenues";

// ── Minimal profile interface (structural — works with bills.ts and bill-financial-view.ts) ──
export interface CashFlowProfile {
  byCurrency: {
    currencyCode: string;
    revenueBestAvailable: number;
    costBestAvailable: number;
    profitBestAvailable: number;
    revenueMaturity: { expectedTotal: number; confirmedTotal: number; actualTotal: number };
    directCostMaturity: { expectedTotal: number; confirmedTotal: number; actualTotal: number };
  }[];
  settlementOutstanding?: {
    currencyCode: string;
    accountsPayableOutstanding: number;
    accountsReceivableOutstanding: number;
  }[];
  reporting?: {
    reportingCurrencyCode: string;
    revenueBestAvailable: number | null;
    costBestAvailable: number | null;
    profitBestAvailable: number | null;
    missingFxCount: number;
    complete: boolean;
  } | null;
}

// ── Colour tokens ─────────────────────────────────────────────────────────────
const C = {
  inflow: "#16a34a",       // green-600
  inflowMid: "#86efac",    // green-300
  inflowLight: "#f0fdf4",  // green-50
  outflow: "#dc2626",      // red-600
  outflowMid: "#fca5a5",   // red-300
  outflowLight: "#fff5f5",
  center: "#2563eb",       // blue-600
  centerLight: "#eff6ff",  // blue-50
  centerBorder: "#93c5fd", // blue-300
  profit: "#7c3aed",       // violet-600
  loss: "#dc2626",
  profitLight: "#f5f3ff",
  lossLight: "#fff5f5",
  text: "#111827",
  muted: "#6b7280",
  border: "#e5e7eb",
  pillExpected: "#fbbf24",
  pillConfirmed: "#3b82f6",
  pillActual: "#22c55e",
} as const;

// ── Layout constants ──────────────────────────────────────────────────────────
const VW = 960;
const NODE_W = 210;     // width of each cost/revenue node
const NODE_H = 56;      // height of each node
const NODE_GAP = 12;    // vertical gap between nodes
const NODE_MARGIN_TOP = 48; // space for column header
const CENTER_W = 180;
const CENTER_H_BASE = 160;  // grows if reporting block shown
const COL_PAD_X = 16;   // padding inside node
const CENTER_X = (VW - CENTER_W) / 2;

// ── helpers ───────────────────────────────────────────────────────────────────
function maturityColor(m: string): string {
  switch (m?.toLowerCase()) {
    case "expected": return C.pillExpected;
    case "confirmed": return C.pillConfirmed;
    case "actual": return C.pillActual;
    default: return C.muted;
  }
}

function maturityVi(m: string): string {
  switch (m?.toLowerCase()) {
    case "expected": return "Dự kiến";
    case "confirmed": return "Xác nhận";
    case "actual": return "Thực tế";
    default: return m ?? "—";
  }
}

// ── Single line-item node ─────────────────────────────────────────────────────
interface LineNodeProps {
  label: string;
  amount: number;
  currencyCode: string;
  maturity: string;
  x: number;
  y: number;
  w: number;
  kind: "inflow" | "outflow";
  inactive?: boolean;
}

function LineNode({ label, amount, currencyCode, maturity, x, y, w, kind, inactive }: LineNodeProps) {
  const fg = kind === "inflow" ? C.inflow : C.outflow;
  const bg = inactive ? "#f9fafb" : (kind === "inflow" ? C.inflowLight : C.outflowLight);
  const borderColor = inactive ? C.border : (kind === "inflow" ? C.inflowMid : C.outflowMid);
  const mColor = maturityColor(maturity);
  const PILL_W = 54;

  return (
    <g opacity={inactive ? 0.5 : 1}>
      {/* card */}
      <rect x={x} y={y} width={w} height={NODE_H} rx={7} fill={bg} stroke={borderColor} strokeWidth={1.5} />
      {/* label */}
      <text x={x + COL_PAD_X} y={y + 18} fontSize={10} fontWeight="600" fill={fg}>
        {label.length > 22 ? label.slice(0, 22) + "…" : label}
      </text>
      {/* amount */}
      <text x={x + w - COL_PAD_X} y={y + 18} fontSize={10} fontWeight="700" fill={C.text} textAnchor="end">
        {formatMoney(amount, currencyCode)}
      </text>
      {/* currency */}
      <text x={x + COL_PAD_X} y={y + 34} fontSize={9} fill={C.muted}>
        {currencyCode}
      </text>
      {/* maturity pill */}
      <rect x={x + w - COL_PAD_X - PILL_W} y={y + 24} width={PILL_W} height={14} rx={4} fill={mColor} opacity={0.15} />
      <text x={x + w - COL_PAD_X - PILL_W / 2} y={y + 34} fontSize={8} fontWeight="600" fill={mColor} textAnchor="middle">
        {maturityVi(maturity)}
      </text>
      {/* direction indicator */}
      <text x={x + COL_PAD_X} y={y + 48} fontSize={8} fill={fg} opacity={0.7}>
        {kind === "inflow" ? "→ BILL" : "BILL →"}
      </text>
    </g>
  );
}

// ── Bezier arrow ──────────────────────────────────────────────────────────────
interface ArrowProps {
  x1: number; y1: number;
  x2: number; y2: number;
  color: string;
  dashed?: boolean;
}

function BezierArrow({ x1, y1, x2, y2, color, dashed }: ArrowProps) {
  // control points: bend outward from center
  const cx1 = x1 + (x2 - x1) * 0.45;
  const cx2 = x1 + (x2 - x1) * 0.55;
  const d = `M${x1},${y1} C${cx1},${y1} ${cx2},${y2} ${x2},${y2}`;
  const markerId = `arr-${color.replace("#", "")}`;
  return (
    <path
      d={d}
      fill="none"
      stroke={color}
      strokeWidth={1.5}
      strokeDasharray={dashed ? "4 3" : undefined}
      strokeOpacity={0.7}
      markerEnd={`url(#${markerId})`}
    />
  );
}

// ── Fallback bucket view (when no line items available) ───────────────────────
function BucketFallback({ profile }: { profile: CashFlowProfile }) {
  return (
    <p style={{ color: C.muted, fontSize: 12, padding: "1rem 0" }}>
      Chưa có dòng chi phí / doanh thu để hiển thị chi tiết.
      {profile.byCurrency.length > 0
        ? ` Tổng: ${profile.byCurrency.map(b => `${formatMoney(b.revenueBestAvailable, b.currencyCode)} DT / ${formatMoney(b.costBestAvailable, b.currencyCode)} CP`).join(" · ")}`
        : ""}
    </p>
  );
}

// ── Main component ────────────────────────────────────────────────────────────
interface Props {
  profile: CashFlowProfile;
  costs?: CostListItem[] | null;
  revenues?: RevenueListItem[] | null;
}

export function BillCashFlowMap({ profile, costs, revenues }: Props) {
  const activeRevenues = (revenues ?? []).filter(r => r.recordStatus === "active");
  const activeCosts = (costs ?? []).filter(c => c.recordStatus === "active");

  // If no line items, show fallback
  if (activeRevenues.length === 0 && activeCosts.length === 0) {
    return <BucketFallback profile={profile} />;
  }

  const reporting = profile.reporting;
  const reportCcy = reporting?.reportingCurrencyCode;
  const bucket0 = profile.byCurrency[0];

  const revenueBest = reporting?.revenueBestAvailable ?? bucket0?.revenueBestAvailable ?? null;
  const costBest = reporting?.costBestAvailable ?? bucket0?.costBestAvailable ?? null;
  const profitVal = reporting?.profitBestAvailable ?? bucket0?.profitBestAvailable ?? null;
  const isLoss = (profitVal ?? 0) < 0;
  const displayCcy = reportCcy ?? bucket0?.currencyCode ?? "VND";
  const margin = revenueBest && revenueBest !== 0 ? ((profitVal ?? 0) / revenueBest) * 100 : null;

  const outstandingMap = Object.fromEntries(
    (profile.settlementOutstanding ?? []).map(s => [
      s.currencyCode,
      { ap: s.accountsPayableOutstanding, ar: s.accountsReceivableOutstanding },
    ])
  );

  // Total AR/AP outstanding (sum across currencies)
  const totalArOutstanding = (profile.settlementOutstanding ?? []).reduce((a, s) => a + s.accountsReceivableOutstanding, 0);
  const totalApOutstanding = (profile.settlementOutstanding ?? []).reduce((a, s) => a + s.accountsPayableOutstanding, 0);

  // Layout: determine heights
  const numLeft = Math.max(1, activeRevenues.length);
  const numRight = Math.max(1, activeCosts.length);
  const numMax = Math.max(numLeft, numRight);
  const colH = numMax * NODE_H + (numMax - 1) * NODE_GAP;
  const centerH = CENTER_H_BASE + (reporting ? 20 : 0) + (totalArOutstanding > 0 || totalApOutstanding > 0 ? 20 : 0);
  const vh = Math.max(colH + NODE_MARGIN_TOP + 60, centerH + NODE_MARGIN_TOP + 60);

  // Vertical positioning
  const leftColH = numLeft * NODE_H + (numLeft - 1) * NODE_GAP;
  const rightColH = numRight * NODE_H + (numRight - 1) * NODE_GAP;
  const centerY = (vh - centerH) / 2;
  const leftStartY = NODE_MARGIN_TOP + (vh - NODE_MARGIN_TOP - leftColH) / 2;
  const rightStartY = NODE_MARGIN_TOP + (vh - NODE_MARGIN_TOP - rightColH) / 2;

  const profitColor = isLoss ? C.loss : C.profit;
  const profitBg = isLoss ? C.lossLight : C.profitLight;

  const LEFT_X = 0;
  const RIGHT_X = VW - NODE_W;

  const markerColors = [C.inflow, C.outflow, C.muted];

  // Center node mid Y for arrows
  const centerMidY = centerY + centerH / 2;
  const centerLeftX = CENTER_X;
  const centerRightX = CENTER_X + CENTER_W;

  return (
    <div style={{ width: "100%", overflowX: "auto" }}>
      <p style={{ fontSize: 11, color: C.muted, marginBottom: 8, marginTop: 0 }}>
        Mỗi dòng doanh thu / chi phí được thể hiện là một nút riêng với mũi tên kết nối vào Bill trung tâm.
        Màu pill: <span style={{ color: C.pillExpected }}>■ Dự kiến</span>{" · "}
        <span style={{ color: C.pillConfirmed }}>■ Xác nhận</span>{" · "}
        <span style={{ color: C.pillActual }}>■ Thực tế</span>.
        Không phải sổ cái.
      </p>
      <svg
        viewBox={`0 0 ${VW} ${vh}`}
        width="100%"
        aria-label="Sơ đồ dòng tiền Bill theo từng khoản"
        role="img"
        style={{ display: "block", maxWidth: VW, minHeight: 280 }}
      >
        <defs>
          {markerColors.map(col => (
            <marker
              key={col}
              id={`arr-${col.replace("#", "")}`}
              markerWidth="7" markerHeight="7"
              refX="5" refY="3"
              orient="auto"
            >
              <path d="M0,0 L0,6 L7,3 z" fill={col} />
            </marker>
          ))}
        </defs>

        {/* ── LEFT column header ──────────────────────────────────── */}
        <text x={LEFT_X + NODE_W / 2} y={24} fontSize={12} fontWeight="700" fill={C.inflow} textAnchor="middle">
          ↙ DÒNG VÀO — Doanh thu ({activeRevenues.length})
        </text>

        {/* ── Revenue nodes + arrows ──────────────────────────────── */}
        {activeRevenues.map((r, i) => {
          const nodeY = leftStartY + i * (NODE_H + NODE_GAP);
          const nodeMidY = nodeY + NODE_H / 2;
          return (
            <g key={r.id}>
              <LineNode
                label={r.revenueTypeCode ?? `Doanh thu #${i + 1}`}
                amount={r.amount}
                currencyCode={r.currencyCode}
                maturity={r.financialMaturity}
                x={LEFT_X}
                y={nodeY}
                w={NODE_W}
                kind="inflow"
              />
              <BezierArrow
                x1={LEFT_X + NODE_W + 2}
                y1={nodeMidY}
                x2={centerLeftX - 2}
                y2={centerMidY}
                color={C.inflow}
              />
            </g>
          );
        })}

        {/* ── Empty left state ────────────────────────────────────── */}
        {activeRevenues.length === 0 && (
          <g>
            <rect x={LEFT_X} y={leftStartY} width={NODE_W} height={NODE_H} rx={7} fill="#f9fafb" stroke={C.border} strokeWidth={1} strokeDasharray="4 3" />
            <text x={LEFT_X + NODE_W / 2} y={leftStartY + NODE_H / 2 + 4} textAnchor="middle" fontSize={10} fill={C.muted}>
              Chưa có doanh thu
            </text>
          </g>
        )}

        {/* ── CENTRE Bill node ────────────────────────────────────── */}
        <rect
          x={CENTER_X} y={centerY}
          width={CENTER_W} height={centerH}
          rx={12}
          fill={profitBg}
          stroke={profitColor}
          strokeWidth={2}
        />
        {/* Bill label */}
        <text x={CENTER_X + CENTER_W / 2} y={centerY + 22} fontSize={12} fontWeight="800" fill={C.center} textAnchor="middle">
          📋 BILL
        </text>
        {/* Divider */}
        <line x1={CENTER_X + 14} y1={centerY + 30} x2={CENTER_X + CENTER_W - 14} y2={centerY + 30} stroke={C.centerBorder} strokeWidth={1} />

        {/* Revenue total */}
        <text x={CENTER_X + 14} y={centerY + 48} fontSize={9} fill={C.muted}>Doanh thu</text>
        <text x={CENTER_X + CENTER_W - 14} y={centerY + 48} fontSize={10} fontWeight="700" fill={C.inflow} textAnchor="end">
          {revenueBest != null ? formatMoney(revenueBest, displayCcy) : "—"}
        </text>

        {/* Cost total */}
        <text x={CENTER_X + 14} y={centerY + 66} fontSize={9} fill={C.muted}>Chi phí</text>
        <text x={CENTER_X + CENTER_W - 14} y={centerY + 66} fontSize={10} fontWeight="700" fill={C.outflow} textAnchor="end">
          {costBest != null ? formatMoney(costBest, displayCcy) : "—"}
        </text>

        {/* Divider 2 */}
        <line x1={CENTER_X + 14} y1={centerY + 76} x2={CENTER_X + CENTER_W - 14} y2={centerY + 76} stroke={C.centerBorder} strokeWidth={1} />

        {/* Profit */}
        <text x={CENTER_X + CENTER_W / 2} y={centerY + 96} fontSize={14} fontWeight="800" fill={profitColor} textAnchor="middle">
          {isLoss ? "▼ Lỗ" : "▲ LN"} {profitVal != null ? formatMoney(Math.abs(profitVal), displayCcy) : "—"}
        </text>
        {margin != null ? (
          <text x={CENTER_X + CENTER_W / 2} y={centerY + 112} fontSize={11} fill={profitColor} textAnchor="middle">
            Biên {margin.toFixed(1)}%
          </text>
        ) : null}

        {/* Currency note */}
        {reportCcy ? (
          <text x={CENTER_X + CENTER_W / 2} y={centerY + 128} fontSize={8} fill={C.muted} textAnchor="middle">
            Quy đổi ({reportCcy})
          </text>
        ) : null}

        {/* Outstanding AP */}
        {totalApOutstanding > 0 ? (
          <>
            <rect x={CENTER_X + 10} y={centerY + 133} width={CENTER_W - 20} height={14} rx={3} fill="#fef3c7" stroke="#f59e0b" strokeWidth={1} />
            <text x={CENTER_X + CENTER_W / 2} y={centerY + 143} fontSize={8} fill="#92400e" textAnchor="middle" fontWeight="600">
              ⚠ Còn phải trả: {formatMoney(totalApOutstanding, displayCcy)}
            </text>
          </>
        ) : null}

        {/* Outstanding AR */}
        {totalArOutstanding > 0 ? (
          <>
            <rect x={CENTER_X + 10} y={centerY + (totalApOutstanding > 0 ? 150 : 133)} width={CENTER_W - 20} height={14} rx={3} fill="#dcfce7" stroke="#16a34a" strokeWidth={1} />
            <text x={CENTER_X + CENTER_W / 2} y={centerY + (totalApOutstanding > 0 ? 160 : 143)} fontSize={8} fill="#14532d" textAnchor="middle" fontWeight="600">
              ⚠ Còn phải thu: {formatMoney(totalArOutstanding, displayCcy)}
            </text>
          </>
        ) : null}

        {reporting && !reporting.complete ? (
          <text x={CENTER_X + CENTER_W / 2} y={centerY + centerH - 8} fontSize={8} fill={C.muted} textAnchor="middle">
            ⚠ Thiếu tỷ giá {reporting.missingFxCount} dòng
          </text>
        ) : null}

        {/* ── RIGHT column header ─────────────────────────────────── */}
        <text x={RIGHT_X + NODE_W / 2} y={24} fontSize={12} fontWeight="700" fill={C.outflow} textAnchor="middle">
          DÒNG RA — Chi phí ({activeCosts.length}) ↗
        </text>

        {/* ── Cost nodes + arrows ─────────────────────────────────── */}
        {activeCosts.map((c, i) => {
          const nodeY = rightStartY + i * (NODE_H + NODE_GAP);
          const nodeMidY = nodeY + NODE_H / 2;
          return (
            <g key={c.id}>
              <LineNode
                label={c.costTypeCode ?? `Chi phí #${i + 1}`}
                amount={c.amount}
                currencyCode={c.currencyCode}
                maturity={c.financialMaturity}
                x={RIGHT_X}
                y={nodeY}
                w={NODE_W}
                kind="outflow"
              />
              <BezierArrow
                x1={centerRightX + 2}
                y1={centerMidY}
                x2={RIGHT_X - 2}
                y2={nodeMidY}
                color={C.outflow}
              />
            </g>
          );
        })}

        {/* ── Empty right state ───────────────────────────────────── */}
        {activeCosts.length === 0 && (
          <g>
            <rect x={RIGHT_X} y={rightStartY} width={NODE_W} height={NODE_H} rx={7} fill="#f9fafb" stroke={C.border} strokeWidth={1} strokeDasharray="4 3" />
            <text x={RIGHT_X + NODE_W / 2} y={rightStartY + NODE_H / 2 + 4} textAnchor="middle" fontSize={10} fill={C.muted}>
              Chưa có chi phí
            </text>
          </g>
        )}

        {/* ── AP/AR outstanding by currency (bottom) ──────────────── */}
        {(profile.settlementOutstanding ?? []).filter(s => s.accountsPayableOutstanding > 0 || s.accountsReceivableOutstanding > 0).map((s, i) => (
          <g key={s.currencyCode}>
            <text
              x={CENTER_X + CENTER_W / 2}
              y={vh - 28 + i * 14}
              fontSize={8}
              fill={C.muted}
              textAnchor="middle"
            >
              {s.currencyCode}: AR tồn {formatMoney(s.accountsReceivableOutstanding, s.currencyCode)} · AP tồn {formatMoney(s.accountsPayableOutstanding, s.currencyCode)}
            </text>
          </g>
        ))}
      </svg>
    </div>
  );
}
