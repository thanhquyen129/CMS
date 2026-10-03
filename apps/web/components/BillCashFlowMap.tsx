/**
 * BillCashFlowMap — SVG-only, 0 external dependencies.
 *
 * Visualises inflow (AR) → Bill centre → outflow (AP) for a single Bill.
 * Data comes directly from BillFinancialProfile already loaded in the drawer.
 *
 * Render model
 * ============
 *   LEFT column  : AR lanes — one per currency bucket  (green)
 *   CENTRE node  : Bill margin / profit card
 *   RIGHT column : AP lanes — one per currency bucket  (red/orange)
 *
 * For each lane we show 3 maturity bars: Expected / Confirmed / Actual.
 * Settlement outstanding is overlaid as a small "⚠ outstanding" badge.
 *
 * The diagram is responsive: the SVG viewBox scales; no fixed px.
 */
"use client";

import { formatMoney } from "@/lib/money";
import type {
  BillFinancialProfile,
  CurrencyFinancialBucket,
  MaturityBreakdown,
} from "@/lib/bill-financial-view";

// ── colours (match CSS design tokens where possible) ──────────────────────────
const C = {
  inflow: "#16a34a",   // green-600
  inflowLight: "#dcfce7", // green-100
  outflow: "#dc2626",  // red-600
  outflowLight: "#fee2e2", // red-100
  center: "#2563eb",   // blue-600
  centerLight: "#dbeafe", // blue-100
  profit: "#7c3aed",   // violet-600
  profitLight: "#ede9fe",
  loss: "#dc2626",
  text: "#111827",
  muted: "#6b7280",
  border: "#e5e7eb",
  arrowStroke: "#9ca3af",
  expected: "#fbbf24",  // amber-400
  confirmed: "#3b82f6", // blue-500
  actual: "#22c55e",    // green-500
} as const;

// ── layout constants ──────────────────────────────────────────────────────────
const VW = 900;          // viewBox width
const VH_BASE = 340;     // base viewBox height (grows with extra currencies)
const COL_W = 240;       // left/right column width
const CENTER_W = 160;    // centre node width
const CENTER_H = 130;    // centre node height
const GAP = 30;          // gap between col and centre
const LANE_H = 80;       // height per currency lane
const LANE_GAP = 12;     // gap between lanes
const ARROW_Y_OFFSET = 40; // vertical mid of an arrow from the lane

// ── helpers ───────────────────────────────────────────────────────────────────
function pct(value: number, total: number): number {
  if (!total || !Number.isFinite(value / total)) return 0;
  return Math.min(100, Math.max(0, (value / total) * 100));
}

function best(b: MaturityBreakdown): number {
  if (b.actualTotal > 0) return b.actualTotal;
  if (b.confirmedTotal > 0) return b.confirmedTotal;
  return b.expectedTotal;
}

interface LaneProps {
  bucket: CurrencyFinancialBucket;
  x: number;
  y: number;
  w: number;
  kind: "inflow" | "outflow";
  outstanding: number;
}

function Lane({ bucket, x, y, w, kind, outstanding }: LaneProps) {
  const maturity =
    kind === "inflow" ? bucket.revenueMaturity : bucket.directCostMaturity;
  const total =
    kind === "inflow"
      ? bucket.revenueBestAvailable
      : bucket.costBestAvailable;
  const fg = kind === "inflow" ? C.inflow : C.outflow;
  const bg = kind === "inflow" ? C.inflowLight : C.outflowLight;
  const bw = w - 16; // bar area width
  const BAR_H = 8;
  const bars = [
    { label: "Dự kiến", value: maturity.expectedTotal, color: C.expected },
    { label: "Xác nhận", value: maturity.confirmedTotal, color: C.confirmed },
    { label: "Thực tế", value: maturity.actualTotal, color: C.actual },
  ];

  return (
    <g>
      {/* lane card */}
      <rect
        x={x}
        y={y}
        width={w}
        height={LANE_H}
        rx={8}
        fill={bg}
        stroke={fg}
        strokeWidth={1.5}
      />
      {/* currency label */}
      <text
        x={x + 8}
        y={y + 16}
        fontSize={11}
        fontWeight="600"
        fill={fg}
      >
        {bucket.currencyCode}
      </text>
      {/* best-available amount */}
      <text
        x={x + w - 8}
        y={y + 16}
        fontSize={11}
        fontWeight="700"
        fill={C.text}
        textAnchor="end"
      >
        {formatMoney(total, bucket.currencyCode)}
      </text>

      {/* maturity bars */}
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

      {/* outstanding badge */}
      {outstanding > 0 ? (
        <g>
          <rect
            x={x + 8}
            y={y + LANE_H - 16}
            width={w - 16}
            height={13}
            rx={3}
            fill="#fef3c7"
            stroke="#f59e0b"
            strokeWidth={1}
          />
          <text
            x={x + 12}
            y={y + LANE_H - 6}
            fontSize={8}
            fill="#92400e"
            fontWeight="600"
          >
            ⚠ Còn dư: {formatMoney(outstanding, bucket.currencyCode)}
          </text>
        </g>
      ) : null}
    </g>
  );
}

// ── Arrow (curved bezier) ─────────────────────────────────────────────────────
interface ArrowProps {
  x1: number; y1: number;
  x2: number; y2: number;
  color: string;
  label?: string;
}

function Arrow({ x1, y1, x2, y2, color, label }: ArrowProps) {
  const mx = (x1 + x2) / 2;
  const d = `M${x1},${y1} C${mx},${y1} ${mx},${y2} ${x2},${y2}`;
  return (
    <g>
      <path
        d={d}
        fill="none"
        stroke={color}
        strokeWidth={2}
        strokeDasharray="4 3"
        markerEnd={`url(#arrow-${color.replace("#", "")})`}
      />
      {label ? (
        <text
          x={mx}
          y={Math.min(y1, y2) - 4}
          fontSize={8}
          fill={color}
          textAnchor="middle"
        >
          {label}
        </text>
      ) : null}
    </g>
  );
}

// ── main component ────────────────────────────────────────────────────────────
interface Props {
  profile: BillFinancialProfile;
}

export function BillCashFlowMap({ profile }: Props) {
  const buckets = profile.byCurrency ?? [];
  const reporting = profile.reporting;
  const reportCcy = reporting?.reportingCurrencyCode;

  // outstanding by currency from settlementOutstanding
  const outstandingMap = Object.fromEntries(
    (profile.settlementOutstanding ?? []).map((s) => [
      s.currencyCode,
      { ap: s.accountsPayableOutstanding, ar: s.accountsReceivableOutstanding },
    ])
  );

  const numBuckets = Math.max(1, buckets.length);
  const colH = numBuckets * LANE_H + (numBuckets - 1) * LANE_GAP;
  const vh = Math.max(VH_BASE, colH + 80);

  // vertical centering of left/right columns vs. centre node
  const centerX = (VW - CENTER_W) / 2;
  const centerY = (vh - CENTER_H) / 2;
  const colStartY = (vh - colH) / 2;

  // reporting profit
  const profitVal =
    reporting?.profitBestAvailable ??
    (buckets[0]?.profitBestAvailable ?? null);
  const revenueBest =
    reporting?.revenueBestAvailable ?? buckets[0]?.revenueBestAvailable ?? null;
  const costBest =
    reporting?.costBestAvailable ?? buckets[0]?.costBestAvailable ?? null;
  const isLoss = (profitVal ?? 0) < 0;
  const profitColor = isLoss ? C.loss : C.profit;
  const profitBg = isLoss ? C.outflowLight : C.profitLight;
  const margin =
    revenueBest && revenueBest !== 0
      ? ((profitVal ?? 0) / revenueBest) * 100
      : null;

  // define arrow markers dynamically
  const markerColors = [C.inflow, C.outflow];

  return (
    <div style={{ width: "100%", overflowX: "auto" }}>
      <p
        style={{
          fontSize: 11,
          color: C.muted,
          marginBottom: 4,
          marginTop: 0,
        }}
      >
        Sơ đồ dòng tiền theo Bill — Trái: Phải thu (AR), Phải: Phải trả (AP).
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

        {/* ── LEFT column: AR (inflow) ────────────────────────────────── */}
        <text
          x={COL_W / 2}
          y={colStartY - 12}
          fontSize={12}
          fontWeight="700"
          fill={C.inflow}
          textAnchor="middle"
        >
          ↙ DÒNG TIỀN VÀO (Phải thu)
        </text>
        {buckets.map((b, i) => {
          const laneY = colStartY + i * (LANE_H + LANE_GAP);
          const os = outstandingMap[b.currencyCode];
          return (
            <Lane
              key={b.currencyCode + "-ar"}
              bucket={b}
              x={0}
              y={laneY}
              w={COL_W}
              kind="inflow"
              outstanding={os?.ar ?? 0}
            />
          );
        })}
        {buckets.length === 0 && (
          <text x={COL_W / 2} y={centerY + 20} textAnchor="middle" fontSize={11} fill={C.muted}>
            Chưa có phải thu
          </text>
        )}

        {/* ── Arrows: AR → Centre ─────────────────────────────────────── */}
        {buckets.map((b, i) => {
          const laneY = colStartY + i * (LANE_H + LANE_GAP);
          return (
            <Arrow
              key={b.currencyCode + "-ar-arrow"}
              x1={COL_W + 4}
              y1={laneY + ARROW_Y_OFFSET}
              x2={centerX - 4}
              y2={centerY + CENTER_H / 2}
              color={C.inflow}
            />
          );
        })}

        {/* ── CENTRE node: Bill ───────────────────────────────────────── */}
        <rect
          x={centerX}
          y={centerY}
          width={CENTER_W}
          height={CENTER_H}
          rx={12}
          fill={profitBg}
          stroke={profitColor}
          strokeWidth={2}
        />
        <text
          x={centerX + CENTER_W / 2}
          y={centerY + 20}
          fontSize={11}
          fontWeight="700"
          fill={C.center}
          textAnchor="middle"
        >
          📋 BILL
        </text>
        {reportCcy ? (
          <>
            <text
              x={centerX + CENTER_W / 2}
              y={centerY + 36}
              fontSize={9}
              fill={C.muted}
              textAnchor="middle"
            >
              Quy đổi ({reportCcy})
            </text>
            <text
              x={centerX + CENTER_W / 2}
              y={centerY + 52}
              fontSize={10}
              fill={C.inflow}
              textAnchor="middle"
            >
              DT: {revenueBest != null ? formatMoney(revenueBest, reportCcy) : "—"}
            </text>
            <text
              x={centerX + CENTER_W / 2}
              y={centerY + 66}
              fontSize={10}
              fill={C.outflow}
              textAnchor="middle"
            >
              CP: {costBest != null ? formatMoney(costBest, reportCcy) : "—"}
            </text>
          </>
        ) : (
          <>
            <text
              x={centerX + CENTER_W / 2}
              y={centerY + 44}
              fontSize={10}
              fill={C.inflow}
              textAnchor="middle"
            >
              DT: {revenueBest != null ? formatMoney(revenueBest, buckets[0]?.currencyCode ?? "") : "—"}
            </text>
            <text
              x={centerX + CENTER_W / 2}
              y={centerY + 60}
              fontSize={10}
              fill={C.outflow}
              textAnchor="middle"
            >
              CP: {costBest != null ? formatMoney(costBest, buckets[0]?.currencyCode ?? "") : "—"}
            </text>
          </>
        )}
        {/* divider */}
        <line
          x1={centerX + 12}
          y1={centerY + 76}
          x2={centerX + CENTER_W - 12}
          y2={centerY + 76}
          stroke={C.border}
          strokeWidth={1}
        />
        {/* profit */}
        <text
          x={centerX + CENTER_W / 2}
          y={centerY + 92}
          fontSize={12}
          fontWeight="800"
          fill={profitColor}
          textAnchor="middle"
        >
          {isLoss ? "▼ Lỗ" : "▲ LN"}{" "}
          {profitVal != null
            ? formatMoney(Math.abs(profitVal), reportCcy ?? buckets[0]?.currencyCode ?? "")
            : "—"}
        </text>
        {margin != null ? (
          <text
            x={centerX + CENTER_W / 2}
            y={centerY + 108}
            fontSize={10}
            fill={profitColor}
            textAnchor="middle"
          >
            Biên {margin.toFixed(1)}%
          </text>
        ) : null}
        {reporting && !reporting.complete ? (
          <text
            x={centerX + CENTER_W / 2}
            y={centerY + 122}
            fontSize={8}
            fill={C.muted}
            textAnchor="middle"
          >
            ⚠ Thiếu tỷ giá
          </text>
        ) : null}

        {/* ── Arrows: Centre → AP ─────────────────────────────────────── */}
        {buckets.map((b, i) => {
          const laneY = colStartY + i * (LANE_H + LANE_GAP);
          return (
            <Arrow
              key={b.currencyCode + "-ap-arrow"}
              x1={centerX + CENTER_W + 4}
              y1={centerY + CENTER_H / 2}
              x2={VW - COL_W - 4}
              y2={laneY + ARROW_Y_OFFSET}
              color={C.outflow}
            />
          );
        })}

        {/* ── RIGHT column: AP (outflow) ──────────────────────────────── */}
        <text
          x={VW - COL_W / 2}
          y={colStartY - 12}
          fontSize={12}
          fontWeight="700"
          fill={C.outflow}
          textAnchor="middle"
        >
          DÒNG TIỀN RA (Phải trả) ↗
        </text>
        {buckets.map((b, i) => {
          const laneY = colStartY + i * (LANE_H + LANE_GAP);
          const os = outstandingMap[b.currencyCode];
          return (
            <Lane
              key={b.currencyCode + "-ap"}
              bucket={b}
              x={VW - COL_W}
              y={laneY}
              w={COL_W}
              kind="outflow"
              outstanding={os?.ap ?? 0}
            />
          );
        })}
        {buckets.length === 0 && (
          <text x={VW - COL_W / 2} y={centerY + 20} textAnchor="middle" fontSize={11} fill={C.muted}>
            Chưa có phải trả
          </text>
        )}

        {/* ── legend ──────────────────────────────────────────────────── */}
        {[
          { color: C.expected, label: "Dự kiến" },
          { color: C.confirmed, label: "Xác nhận" },
          { color: C.actual, label: "Thực tế" },
        ].map((item, i) => (
          <g key={item.label} transform={`translate(${centerX + i * 52}, ${vh - 18})`}>
            <rect width={10} height={10} rx={2} fill={item.color} y={-10} />
            <text x={13} fontSize={9} fill={C.muted}>
              {item.label}
            </text>
          </g>
        ))}
      </svg>
    </div>
  );
}
