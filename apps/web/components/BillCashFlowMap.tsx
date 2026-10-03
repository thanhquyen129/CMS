"use client";
/**
 * BillCashFlowMap — SVG-only, 0 external dependencies.
 *
 * Accepts a minimal CashFlowProfile interface so it works with data from:
 *   - BillFinancialView drawer  (@/lib/bill-financial-view BillFinancialProfile)
 *   - Bill detail page          (@/lib/bills BillFinancialProfile)
 * Both types satisfy the minimal interface below via structural typing.
 */

import { formatMoney } from "@/lib/money";

// ── Minimal interface — both profile types satisfy this ────────────────────────
export interface CashFlowBucket {
  currencyCode: string;
  revenueBestAvailable: number;
  costBestAvailable: number;
  profitBestAvailable: number;
  revenueMaturity: { expectedTotal: number; confirmedTotal: number; actualTotal: number };
  directCostMaturity: { expectedTotal: number; confirmedTotal: number; actualTotal: number };
}

export interface CashFlowProfile {
  byCurrency: CashFlowBucket[];
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

// ── colours ────────────────────────────────────────────────────────────────────
const C = {
  inflow: "#16a34a",
  inflowLight: "#dcfce7",
  outflow: "#dc2626",
  outflowLight: "#fee2e2",
  center: "#2563eb",
  centerLight: "#dbeafe",
  profit: "#7c3aed",
  profitLight: "#ede9fe",
  loss: "#dc2626",
  text: "#111827",
  muted: "#6b7280",
  border: "#e5e7eb",
  expected: "#fbbf24",
  confirmed: "#3b82f6",
  actual: "#22c55e",
} as const;

// ── layout ─────────────────────────────────────────────────────────────────────
const VW = 900;
const COL_W = 240;
const CENTER_W = 160;
const CENTER_H = 130;
const LANE_H = 80;
const LANE_GAP = 12;
const ARROW_Y_OFFSET = 40;

function pct(value: number, total: number): number {
  if (!total || !Number.isFinite(value / total)) return 0;
  return Math.min(100, Math.max(0, (value / total) * 100));
}

interface LaneProps {
  bucket: CashFlowBucket;
  x: number;
  y: number;
  w: number;
  kind: "inflow" | "outflow";
  outstanding: number;
}

function Lane({ bucket, x, y, w, kind, outstanding }: LaneProps) {
  const maturity = kind === "inflow" ? bucket.revenueMaturity : bucket.directCostMaturity;
  const total = kind === "inflow" ? bucket.revenueBestAvailable : bucket.costBestAvailable;
  const fg = kind === "inflow" ? C.inflow : C.outflow;
  const bg = kind === "inflow" ? C.inflowLight : C.outflowLight;
  const bw = w - 16;
  const BAR_H = 8;
  const bars = [
    { label: "Dự kiến", value: maturity.expectedTotal, color: C.expected },
    { label: "Xác nhận", value: maturity.confirmedTotal, color: C.confirmed },
    { label: "Thực tế", value: maturity.actualTotal, color: C.actual },
  ];

  return (
    <g>
      <rect x={x} y={y} width={w} height={LANE_H} rx={8} fill={bg} stroke={fg} strokeWidth={1.5} />
      <text x={x + 8} y={y + 16} fontSize={11} fontWeight="600" fill={fg}>
        {bucket.currencyCode}
      </text>
      <text x={x + w - 8} y={y + 16} fontSize={11} fontWeight="700" fill={C.text} textAnchor="end">
        {formatMoney(total, bucket.currencyCode)}
      </text>
      {bars.map((bar, i) => {
        const barY = y + 24 + i * (BAR_H + 6);
        const fillW = (pct(bar.value, total) / 100) * bw;
        return (
          <g key={bar.label}>
            <rect x={x + 8} y={barY} width={bw} height={BAR_H} rx={3} fill={C.border} />
            <rect x={x + 8} y={barY} width={fillW} height={BAR_H} rx={3} fill={bar.color} />
            <text x={x + 8} y={barY + BAR_H + 10} fontSize={8} fill={C.muted}>
              {bar.label}: {formatMoney(bar.value, bucket.currencyCode)}
            </text>
          </g>
        );
      })}
      {outstanding > 0 ? (
        <g>
          <rect x={x + 8} y={y + LANE_H - 16} width={w - 16} height={13} rx={3} fill="#fef3c7" stroke="#f59e0b" strokeWidth={1} />
          <text x={x + 12} y={y + LANE_H - 6} fontSize={8} fill="#92400e" fontWeight="600">
            ⚠ Còn dư: {formatMoney(outstanding, bucket.currencyCode)}
          </text>
        </g>
      ) : null}
    </g>
  );
}

interface ArrowProps {
  x1: number; y1: number;
  x2: number; y2: number;
  color: string;
}

function Arrow({ x1, y1, x2, y2, color }: ArrowProps) {
  const mx = (x1 + x2) / 2;
  const d = `M${x1},${y1} C${mx},${y1} ${mx},${y2} ${x2},${y2}`;
  const markerId = `arrow-${color.replace("#", "")}`;
  return (
    <path
      d={d}
      fill="none"
      stroke={color}
      strokeWidth={2}
      strokeDasharray="5 4"
      markerEnd={`url(#${markerId})`}
    />
  );
}

interface Props {
  profile: CashFlowProfile;
}

export function BillCashFlowMap({ profile }: Props) {
  const buckets = profile.byCurrency ?? [];
  const reporting = profile.reporting;
  const reportCcy = reporting?.reportingCurrencyCode;

  const outstandingMap = Object.fromEntries(
    (profile.settlementOutstanding ?? []).map((s) => [
      s.currencyCode,
      { ap: s.accountsPayableOutstanding, ar: s.accountsReceivableOutstanding },
    ])
  );

  const numBuckets = Math.max(1, buckets.length);
  const colH = numBuckets * LANE_H + (numBuckets - 1) * LANE_GAP;
  const vh = Math.max(340, colH + 80);

  const centerX = (VW - CENTER_W) / 2;
  const centerY = (vh - CENTER_H) / 2;
  const colStartY = (vh - colH) / 2;

  const profitVal = reporting?.profitBestAvailable ?? buckets[0]?.profitBestAvailable ?? null;
  const revenueBest = reporting?.revenueBestAvailable ?? buckets[0]?.revenueBestAvailable ?? null;
  const costBest = reporting?.costBestAvailable ?? buckets[0]?.costBestAvailable ?? null;
  const isLoss = (profitVal ?? 0) < 0;
  const profitColor = isLoss ? C.loss : C.profit;
  const profitBg = isLoss ? C.outflowLight : C.profitLight;
  const displayCcy = reportCcy ?? buckets[0]?.currencyCode ?? "";
  const margin =
    revenueBest && revenueBest !== 0 ? ((profitVal ?? 0) / revenueBest) * 100 : null;

  const markerColors = [C.inflow, C.outflow];

  return (
    <div style={{ width: "100%", overflowX: "auto" }}>
      <p style={{ fontSize: 11, color: C.muted, marginBottom: 6, marginTop: 0 }}>
        Trái: Phải thu (AR — dòng tiền vào) · Phải: Phải trả (AP — dòng tiền ra) ·
        Thanh màu: Dự kiến / Xác nhận / Thực tế. Không phải sổ cái.
      </p>
      <svg
        viewBox={`0 0 ${VW} ${vh}`}
        width="100%"
        aria-label="Sơ đồ dòng tiền Bill"
        role="img"
        style={{ display: "block", maxWidth: VW }}
      >
        <defs>
          {markerColors.map((col) => (
            <marker
              key={col}
              id={`arrow-${col.replace("#", "")}`}
              markerWidth="8"
              markerHeight="8"
              refX="6"
              refY="3"
              orient="auto"
            >
              <path d="M0,0 L0,6 L8,3 z" fill={col} />
            </marker>
          ))}
        </defs>

        {/* ── LEFT: AR inflow ─────────────────────────────────────── */}
        <text x={COL_W / 2} y={colStartY - 12} fontSize={12} fontWeight="700" fill={C.inflow} textAnchor="middle">
          ↙ DÒNG TIỀN VÀO (Phải thu)
        </text>
        {buckets.length === 0 ? (
          <text x={COL_W / 2} y={centerY + 20} textAnchor="middle" fontSize={11} fill={C.muted}>
            Chưa có phải thu
          </text>
        ) : (
          buckets.map((b, i) => {
            const laneY = colStartY + i * (LANE_H + LANE_GAP);
            const os = outstandingMap[b.currencyCode];
            return (
              <Lane key={b.currencyCode + "-ar"} bucket={b} x={0} y={laneY} w={COL_W} kind="inflow" outstanding={os?.ar ?? 0} />
            );
          })
        )}

        {/* ── Arrows AR → Centre ──────────────────────────────────── */}
        {buckets.map((b, i) => {
          const laneY = colStartY + i * (LANE_H + LANE_GAP);
          return (
            <Arrow key={b.currencyCode + "-ar-arrow"} x1={COL_W + 4} y1={laneY + ARROW_Y_OFFSET} x2={centerX - 4} y2={centerY + CENTER_H / 2} color={C.inflow} />
          );
        })}

        {/* ── CENTRE: Bill ────────────────────────────────────────── */}
        <rect x={centerX} y={centerY} width={CENTER_W} height={CENTER_H} rx={12} fill={profitBg} stroke={profitColor} strokeWidth={2} />
        <text x={centerX + CENTER_W / 2} y={centerY + 20} fontSize={11} fontWeight="700" fill={C.center} textAnchor="middle">
          📋 BILL
        </text>
        {reportCcy ? (
          <text x={centerX + CENTER_W / 2} y={centerY + 34} fontSize={9} fill={C.muted} textAnchor="middle">
            Quy đổi ({reportCcy})
          </text>
        ) : null}
        <text x={centerX + CENTER_W / 2} y={centerY + (reportCcy ? 50 : 44)} fontSize={10} fill={C.inflow} textAnchor="middle">
          DT: {revenueBest != null ? formatMoney(revenueBest, displayCcy) : "—"}
        </text>
        <text x={centerX + CENTER_W / 2} y={centerY + (reportCcy ? 64 : 60)} fontSize={10} fill={C.outflow} textAnchor="middle">
          CP: {costBest != null ? formatMoney(costBest, displayCcy) : "—"}
        </text>
        <line x1={centerX + 12} y1={centerY + 76} x2={centerX + CENTER_W - 12} y2={centerY + 76} stroke={C.border} strokeWidth={1} />
        <text x={centerX + CENTER_W / 2} y={centerY + 92} fontSize={12} fontWeight="800" fill={profitColor} textAnchor="middle">
          {isLoss ? "▼ Lỗ" : "▲ LN"} {profitVal != null ? formatMoney(Math.abs(profitVal), displayCcy) : "—"}
        </text>
        {margin != null ? (
          <text x={centerX + CENTER_W / 2} y={centerY + 108} fontSize={10} fill={profitColor} textAnchor="middle">
            Biên {margin.toFixed(1)}%
          </text>
        ) : null}
        {reporting && !reporting.complete ? (
          <text x={centerX + CENTER_W / 2} y={centerY + 122} fontSize={8} fill={C.muted} textAnchor="middle">
            ⚠ Thiếu tỷ giá
          </text>
        ) : null}

        {/* ── Arrows Centre → AP ──────────────────────────────────── */}
        {buckets.map((b, i) => {
          const laneY = colStartY + i * (LANE_H + LANE_GAP);
          return (
            <Arrow key={b.currencyCode + "-ap-arrow"} x1={centerX + CENTER_W + 4} y1={centerY + CENTER_H / 2} x2={VW - COL_W - 4} y2={laneY + ARROW_Y_OFFSET} color={C.outflow} />
          );
        })}

        {/* ── RIGHT: AP outflow ───────────────────────────────────── */}
        <text x={VW - COL_W / 2} y={colStartY - 12} fontSize={12} fontWeight="700" fill={C.outflow} textAnchor="middle">
          DÒNG TIỀN RA (Phải trả) ↗
        </text>
        {buckets.length === 0 ? (
          <text x={VW - COL_W / 2} y={centerY + 20} textAnchor="middle" fontSize={11} fill={C.muted}>
            Chưa có phải trả
          </text>
        ) : (
          buckets.map((b, i) => {
            const laneY = colStartY + i * (LANE_H + LANE_GAP);
            const os = outstandingMap[b.currencyCode];
            return (
              <Lane key={b.currencyCode + "-ap"} bucket={b} x={VW - COL_W} y={laneY} w={COL_W} kind="outflow" outstanding={os?.ap ?? 0} />
            );
          })
        )}

        {/* ── legend ──────────────────────────────────────────────── */}
        {[
          { color: C.expected, label: "Dự kiến" },
          { color: C.confirmed, label: "Xác nhận" },
          { color: C.actual, label: "Thực tế" },
        ].map((item, i) => (
          <g key={item.label} transform={`translate(${centerX + i * 56}, ${vh - 18})`}>
            <rect width={10} height={10} rx={2} fill={item.color} y={-10} />
            <text x={13} fontSize={9} fill={C.muted}>{item.label}</text>
          </g>
        ))}
      </svg>
    </div>
  );
}
