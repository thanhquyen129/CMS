import type { ReactNode } from "react";
import { QuerySelectLink } from "@/components/QuerySelectLink";
import { formatMoney } from "@/lib/money";
import {
  remainingLabel,
  type DeadlineRow,
  type RankRow,
} from "@/lib/list-workspace";

export function MiniWidget({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="list-mini-card">
      <div className="data-table-shell-head">
        <h2 className="data-table-shell-title">{title}</h2>
      </div>
      {children}
    </section>
  );
}

export function DeadlineList({
  rows,
  hrefFor,
  empty,
}: {
  rows: DeadlineRow[];
  hrefFor: (id: string) => string;
  empty: string;
}) {
  if (rows.length === 0) {
    return (
      <p className="muted" role="status">
        {empty}
      </p>
    );
  }
  return (
    <>
      <div className="deadline-head">
        <span>Mã</span>
        <span>Khách hàng</span>
        <span>Hạng mục</span>
        <span>Còn lại</span>
      </div>
      {rows.map((r) => {
        const remain = remainingLabel(r.days);
        return (
          <div key={r.id} className="deadline-row">
            <QuerySelectLink className="row-link" href={hrefFor(r.id)}>
              {r.code}
            </QuerySelectLink>
            <span>{r.party || "—"}</span>
            <span>{r.item}</span>
            <span className={`deadline-remain-${remain.tone}`}>{remain.text}</span>
          </div>
        );
      })}
    </>
  );
}

export function RankList({
  rows,
  empty,
}: {
  rows: RankRow[];
  empty: string;
}) {
  if (rows.length === 0) {
    return (
      <p className="muted" role="status">
        {empty}
      </p>
    );
  }
  return (
    <>
      {rows.map((r) => (
        <div key={r.name} className="rank-row">
          <span>{r.name}</span>
          <div className="rank-bar" aria-hidden="true">
            <span style={{ width: `${r.pct}%` }} />
          </div>
          <span>{formatMoney(r.amount, r.currency)}</span>
        </div>
      ))}
    </>
  );
}
