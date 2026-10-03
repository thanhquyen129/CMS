"use client";
/**
 * OrderCashFlowMap — Pure SVG, 0 external dependencies.
 *
 * Visualises the consolidated multi-bill cash flow for an Order:
 *   LEFT column   : Customer inflow / AR status
 *   CENTRE node   : Consolidated Order card (Total Revenue, Cost, Profit, Margin)
 *   RIGHT column  : Related Bills (each shows Bill No, Cost, Revenue, Status) with bezier arrows
 */

import Link from "next/link";
import { formatMoney } from "@/lib/money";

export interface OrderBillFinancialSummary {
  billId: string;
  billNo: string;
  operationalStatus: string;
  currencyCode: string;
  revenue: number;
  cost: number;
  profit: number;
}

interface Props {
  orderNo: string;
  customerName?: string | null;
  bills: OrderBillFinancialSummary[];
}

const C = {
  inflow: "#16a34a",
  inflowLight: "#f0fdf4",
  inflowMid: "#86efac",
  outflow: "#dc2626",
  outflowLight: "#fff5f5",
  outflowMid: "#fca5a5",
  center: "#2563eb",
  centerLight: "#eff6ff",
  centerBorder: "#93c5fd",
  profit: "#7c3aed",
  loss: "#dc2626",
  profitLight: "#f5f3ff",
  lossLight: "#fff5f5",
  text: "#111827",
  muted: "#6b7280",
  border: "#e5e7eb",
};

const VW = 960;
const NODE_W = 230;
const NODE_H = 64;
const NODE_GAP = 12;
const CENTER_W = 200;
const CENTER_H = 150;
const LEFT_X = 0;
const CENTER_X = (VW - CENTER_W) / 2;
const RIGHT_X = VW - NODE_W;

export function OrderCashFlowMap({ orderNo, customerName, bills }: Props) {
  const currency = bills[0]?.currencyCode || "VND";
  const totalRevenue = bills.reduce((acc, b) => acc + b.revenue, 0);
  const totalCost = bills.reduce((acc, b) => acc + b.cost, 0);
  const totalProfit = totalRevenue - totalCost;
  const margin = totalRevenue > 0 ? (totalProfit / totalRevenue) * 100 : null;
  const isLoss = totalProfit < 0;

  const numRight = Math.max(1, bills.length);
  const rightColH = numRight * NODE_H + (numRight - 1) * NODE_GAP;
  const vh = Math.max(280, rightColH + 90, CENTER_H + 90);

  const centerY = (vh - CENTER_H) / 2;
  const leftY = (vh - 100) / 2;
  const rightStartY = (vh - rightColH) / 2;

  const centerMidY = centerY + CENTER_H / 2;
  const centerLeftX = CENTER_X;
  const centerRightX = CENTER_X + CENTER_W;

  return (
    <div style={{ width: "100%", overflowX: "auto" }}>
      <p style={{ fontSize: 11, color: C.muted, marginBottom: 8, marginTop: 0 }}>
        Sơ đồ dòng tiền hợp nhất theo Đơn hàng: Thu từ khách hàng → Đơn hàng → Phân bổ ra các Bill vận hành.
      </p>
      <svg
        viewBox={`0 0 ${VW} ${vh}`}
        width="100%"
        aria-label="Sơ đồ dòng tiền Đơn hàng"
        role="img"
        style={{ display: "block", maxWidth: VW, minHeight: 250 }}
      >
        <defs>
          <marker id="arr-inflow" markerWidth="7" markerHeight="7" refX="5" refY="3" orient="auto">
            <path d="M0,0 L0,6 L7,3 z" fill={C.inflow} />
          </marker>
          <marker id="arr-outflow" markerWidth="7" markerHeight="7" refX="5" refY="3" orient="auto">
            <path d="M0,0 L0,6 L7,3 z" fill={C.outflow} />
          </marker>
        </defs>

        {/* ── LEFT: Customer Inflow ── */}
        <text x={LEFT_X + NODE_W / 2} y={leftY - 14} fontSize={12} fontWeight="700" fill={C.inflow} textAnchor="middle">
          ↙ DÒNG THU TỪ KHÁCH HÀNG
        </text>
        <g>
          <rect x={LEFT_X} y={leftY} width={NODE_W} height={100} rx={8} fill={C.inflowLight} stroke={C.inflowMid} strokeWidth={1.5} />
          <text x={LEFT_X + 14} y={leftY + 22} fontSize={11} fontWeight="700" fill={C.inflow}>
            {customerName || "Khách hàng"}
          </text>
          <text x={LEFT_X + 14} y={leftY + 40} fontSize={9} fill={C.muted}>
            Doanh thu kế hoạch
          </text>
          <text x={LEFT_X + 14} y={leftY + 58} fontSize={13} fontWeight="800" fill={C.text}>
            {formatMoney(totalRevenue, currency)}
          </text>
          <text x={LEFT_X + 14} y={leftY + 80} fontSize={9} fill={C.muted}>
            Gắn {bills.length} Bill vận chuyển
          </text>
        </g>

        {/* Arrow Left -> Center */}
        <path
          d={`M${LEFT_X + NODE_W + 2},${leftY + 50} C${LEFT_X + NODE_W + 60},${leftY + 50} ${centerLeftX - 60},${centerMidY} ${centerLeftX - 2},${centerMidY}`}
          fill="none"
          stroke={C.inflow}
          strokeWidth={2}
          strokeDasharray="5 4"
          markerEnd="url(#arr-inflow)"
        />

        {/* ── CENTER: Order Node ── */}
        <rect
          x={CENTER_X}
          y={centerY}
          width={CENTER_W}
          height={CENTER_H}
          rx={12}
          fill={isLoss ? C.lossLight : C.profitLight}
          stroke={isLoss ? C.loss : C.profit}
          strokeWidth={2}
        />
        <text x={CENTER_X + CENTER_W / 2} y={centerY + 22} fontSize={12} fontWeight="800" fill={C.center} textAnchor="middle">
          📦 ĐƠN HÀNG: {orderNo}
        </text>
        <line x1={CENTER_X + 14} y1={centerY + 30} x2={CENTER_X + CENTER_W - 14} y2={centerY + 30} stroke={C.centerBorder} strokeWidth={1} />

        <text x={CENTER_X + 14} y={centerY + 48} fontSize={9} fill={C.muted}>Tổng doanh thu</text>
        <text x={CENTER_X + CENTER_W - 14} y={centerY + 48} fontSize={10} fontWeight="700" fill={C.inflow} textAnchor="end">
          {formatMoney(totalRevenue, currency)}
        </text>

        <text x={CENTER_X + 14} y={centerY + 66} fontSize={9} fill={C.muted}>Tổng chi phí</text>
        <text x={CENTER_X + CENTER_W - 14} y={centerY + 66} fontSize={10} fontWeight="700" fill={C.outflow} textAnchor="end">
          {formatMoney(totalCost, currency)}
        </text>

        <line x1={CENTER_X + 14} y1={centerY + 76} x2={CENTER_X + CENTER_W - 14} y2={centerY + 76} stroke={C.centerBorder} strokeWidth={1} />

        <text x={CENTER_X + CENTER_W / 2} y={centerY + 98} fontSize={15} fontWeight="800" fill={isLoss ? C.loss : C.profit} textAnchor="middle">
          {isLoss ? "▼ Lỗ" : "▲ LN"} {formatMoney(Math.abs(totalProfit), currency)}
        </text>
        {margin != null ? (
          <text x={CENTER_X + CENTER_W / 2} y={centerY + 116} fontSize={11} fill={isLoss ? C.loss : C.profit} textAnchor="middle">
            Biên LN: {margin.toFixed(1)}%
          </text>
        ) : null}
        <text x={CENTER_X + CENTER_W / 2} y={centerY + 134} fontSize={8} fill={C.muted} textAnchor="middle">
          Hợp nhất từ {bills.length} Bill
        </text>

        {/* ── RIGHT: Bills Outflow / Allocation ── */}
        <text x={RIGHT_X + NODE_W / 2} y={rightStartY - 14} fontSize={12} fontWeight="700" fill={C.outflow} textAnchor="middle">
          CÁC BILL LIÊN KẾT ({bills.length}) ↗
        </text>

        {bills.length === 0 ? (
          <g>
            <rect x={RIGHT_X} y={rightStartY} width={NODE_W} height={NODE_H} rx={7} fill="#f9fafb" stroke={C.border} strokeWidth={1} strokeDasharray="4 3" />
            <text x={RIGHT_X + NODE_W / 2} y={rightStartY + NODE_H / 2 + 4} textAnchor="middle" fontSize={10} fill={C.muted}>
              Chưa liên kết Bill nào
            </text>
          </g>
        ) : (
          bills.map((b, i) => {
            const nodeY = rightStartY + i * (NODE_H + NODE_GAP);
            const nodeMidY = nodeY + NODE_H / 2;
            return (
              <g key={b.billId}>
                <rect x={RIGHT_X} y={nodeY} width={NODE_W} height={NODE_H} rx={7} fill={C.outflowLight} stroke={C.outflowMid} strokeWidth={1.5} />
                <text x={RIGHT_X + 12} y={nodeY + 18} fontSize={11} fontWeight="700" fill={C.center}>
                  {b.billNo}
                </text>
                <text x={RIGHT_X + NODE_W - 12} y={nodeY + 18} fontSize={9} fill={C.muted} textAnchor="end">
                  {b.operationalStatus}
                </text>
                <text x={RIGHT_X + 12} y={nodeY + 36} fontSize={9} fill={C.muted}>
                  CP: {formatMoney(b.cost, b.currencyCode)}
                </text>
                <text x={RIGHT_X + NODE_W - 12} y={nodeY + 36} fontSize={9} fill={C.inflow} textAnchor="end" fontWeight="600">
                  DT: {formatMoney(b.revenue, b.currencyCode)}
                </text>
                <text x={RIGHT_X + 12} y={nodeY + 52} fontSize={9} fill={b.profit >= 0 ? C.profit : C.loss} fontWeight="600">
                  LN: {formatMoney(b.profit, b.currencyCode)}
                </text>

                {/* Arrow Center -> Bill */}
                <path
                  d={`M${centerRightX + 2},${centerMidY} C${centerRightX + 50},${centerMidY} ${RIGHT_X - 50},${nodeMidY} ${RIGHT_X - 2},${nodeMidY}`}
                  fill="none"
                  stroke={C.outflow}
                  strokeWidth={1.5}
                  strokeDasharray="4 3"
                  markerEnd="url(#arr-outflow)"
                />
              </g>
            );
          })
        )}
      </svg>
    </div>
  );
}
