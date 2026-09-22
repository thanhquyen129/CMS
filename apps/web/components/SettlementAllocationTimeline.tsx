import type { ReactNode } from "react";
import { allocationStatusLabel } from "@/lib/settlements";
import type { TerminologyMap } from "@/lib/terminology";
import { formatDateTimeVi, formatMoney } from "@/lib/money";

export type SettlementTimelineAlloc = {
  id: string;
  amount: number;
  currencyCode: string;
  allocationStatus: string;
  createdAt: string;
  finalizedAt: string | null;
  reversedAt: string | null;
  reverseReason: string | null;
  targetId: string;
};

type Props = {
  terms: TerminologyMap;
  /** Thanh toán / Thu tiền */
  cashLabel: string;
  /** AP / AR short label for target line */
  targetLabel: string;
  valueDate: string;
  cashAmount: number;
  currencyCode: string;
  allocations: SettlementTimelineAlloc[];
  renderActions: (alloc: SettlementTimelineAlloc) => ReactNode;
};

function statusClass(status: string): string {
  switch (status?.toLowerCase()) {
    case "finalized":
      return "is-done";
    case "reversed":
      return "is-reversed";
    default:
      return "is-draft";
  }
}

function eventAt(a: SettlementTimelineAlloc): string {
  if (a.reversedAt) return formatDateTimeVi(a.reversedAt);
  if (a.finalizedAt) return formatDateTimeVi(a.finalizedAt);
  if (a.createdAt) return formatDateTimeVi(a.createdAt);
  return "—";
}

/** W-L4: readable allocate timeline (cash → phân bổ) vs flat table alone. */
export function SettlementAllocationTimeline({
  terms,
  cashLabel,
  targetLabel,
  valueDate,
  cashAmount,
  currencyCode,
  allocations,
  renderActions,
}: Props) {
  const sorted = [...allocations].sort((a, b) => {
    const ta = Date.parse(a.createdAt) || 0;
    const tb = Date.parse(b.createdAt) || 0;
    return ta - tb;
  });

  return (
    <ol className="settlement-timeline" aria-label="Tiến trình phân bổ">
      <li className="settlement-timeline-item is-done">
        <span className="settlement-timeline-marker" aria-hidden="true" />
        <div className="settlement-timeline-body">
          <div className="settlement-timeline-head">
            <strong>Ghi nhận {cashLabel.toLowerCase()}</strong>
            <span className="status-pill status-completed">Đã ghi</span>
          </div>
          <span className="muted small block">
            Ngày giá trị: {formatDateTimeVi(valueDate)}
          </span>
          <div className="settlement-timeline-amount">
            {formatMoney(cashAmount, currencyCode)}
          </div>
        </div>
      </li>

      {sorted.length === 0 ? (
        <li className="settlement-timeline-item is-pending">
          <span className="settlement-timeline-marker" aria-hidden="true" />
          <div className="settlement-timeline-body">
            <strong>Chưa phân bổ</strong>
            <span className="muted small block">
              Tạo phân bổ nháp bên dưới — chỉ phân bổ đã chốt mới giảm outstanding.
            </span>
          </div>
        </li>
      ) : (
        sorted.map((a) => (
          <li
            key={a.id}
            className={`settlement-timeline-item ${statusClass(a.allocationStatus)}`}
          >
            <span className="settlement-timeline-marker" aria-hidden="true" />
            <div className="settlement-timeline-body">
              <div className="settlement-timeline-head">
                <strong>
                  Phân bổ → {targetLabel}{" "}
                  <code className="mono-id">{a.targetId.slice(0, 8)}…</code>
                </strong>
                <span
                  className={
                    a.allocationStatus?.toLowerCase() === "finalized"
                      ? "status-pill status-completed"
                      : a.allocationStatus?.toLowerCase() === "reversed"
                        ? "status-pill status-blocked"
                        : "status-pill"
                  }
                >
                  {allocationStatusLabel(terms, a.allocationStatus)}
                </span>
              </div>
              <span className="muted small block">{eventAt(a)}</span>
              <div className="settlement-timeline-amount">
                {formatMoney(a.amount, a.currencyCode)}
              </div>
              {a.reverseReason ? (
                <span className="muted small block">
                  Lý do đảo: {a.reverseReason}
                </span>
              ) : null}
              <div className="row-actions settlement-timeline-actions">
                {renderActions(a)}
              </div>
            </div>
          </li>
        ))
      )}
    </ol>
  );
}
