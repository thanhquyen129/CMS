import type { ReactNode } from "react";
import Link from "next/link";

export type StatCardModel = {
  key: string;
  label: ReactNode;
  value: ReactNode;
  hint?: ReactNode;
  href?: string;
  tone?: "default" | "danger" | "success";
};

export function StatCardGrid({
  cards,
  className = "stat-grid",
}: {
  cards: StatCardModel[];
  className?: string;
}) {
  if (cards.length === 0) return null;
  return (
    <div className={className} style={{ marginTop: "0.85rem" }}>
      {cards.map((c) => {
        const inner = (
          <>
            <span className="stat-label">{c.label}</span>
            <strong
              className={
                c.tone === "danger"
                  ? "stat-value neg"
                  : c.tone === "success"
                    ? "stat-value"
                    : "stat-value"
              }
            >
              {c.value}
            </strong>
            {c.hint ? <span className="stat-hint">{c.hint}</span> : null}
          </>
        );
        return c.href ? (
          <Link key={c.key} className="stat-card" href={c.href}>
            {inner}
          </Link>
        ) : (
          <div key={c.key} className="stat-card">
            {inner}
          </div>
        );
      })}
    </div>
  );
}
