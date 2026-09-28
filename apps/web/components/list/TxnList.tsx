import type { ReactNode } from "react";

type TxnItemProps = {
  title: ReactNode;
  amount?: ReactNode;
  amountTone?: "neg" | "pos" | null;
  /** Second line, e.g. "Số dư sau 250,00 US$". */
  sub?: ReactNode;
  status?: ReactNode;
  /** Date · actor line. */
  meta?: ReactNode;
  /** Clamped to two lines; full text stays available in the detail. */
  reason?: string | null;
  actions?: ReactNode;
  detail?: ReactNode;
  selected?: boolean;
};

export function TxnList({ children, label }: { children: ReactNode; label?: string }) {
  return (
    <ul className="txn-list" aria-label={label}>
      {children}
    </ul>
  );
}

/** Compact transaction card: type + amount, balance + status, date/actor, short reason, open action. */
export function TxnItem({
  title,
  amount,
  amountTone = null,
  sub,
  status,
  meta,
  reason,
  actions,
  detail,
  selected = false,
}: TxnItemProps) {
  const trimmed = reason?.trim();
  return (
    <li className={selected ? "txn-item is-selected" : "txn-item"}>
      <div className="txn-row">
        <span className="txn-title">{title}</span>
        {amount != null ? (
          <span className={amountTone ? `txn-amount ${amountTone}` : "txn-amount"}>{amount}</span>
        ) : null}
      </div>
      {sub != null || status != null ? (
        <div className="txn-row txn-sub">
          <span>{sub}</span>
          {status}
        </div>
      ) : null}
      {meta != null ? <div className="txn-meta">{meta}</div> : null}
      {trimmed ? (
        <div className="txn-reason" title={trimmed}>
          {trimmed}
        </div>
      ) : null}
      {actions ? <div className="txn-actions">{actions}</div> : null}
      {detail ? <div className="txn-detail">{detail}</div> : null}
    </li>
  );
}
