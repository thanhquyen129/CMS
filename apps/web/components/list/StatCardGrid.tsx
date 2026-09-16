import type { ReactNode } from "react";
import Link from "next/link";

export type StatCardModel = {
  key: string;
  label: ReactNode;
  value: ReactNode;
  hint?: ReactNode;
  href?: string;
  tone?: "default" | "danger" | "success" | "warning" | "info" | "primary";
};

const TONE_CLASS: Record<NonNullable<StatCardModel["tone"]>, string> = {
  default: "stat-card-tone-default",
  danger: "stat-card-tone-danger",
  success: "stat-card-tone-success",
  warning: "stat-card-tone-warning",
  info: "stat-card-tone-info",
  primary: "stat-card-tone-primary",
};

export function StatCardGrid({
  cards,
  className = "stat-grid designer-kpi",
}: {
  cards: StatCardModel[];
  className?: string;
}) {
  if (cards.length === 0) return null;
  return (
    <div className={className}>
      {cards.map((c) => {
        const tone = c.tone ?? "default";
        const inner = (
          <>
            <span className={`stat-card-icon ${TONE_CLASS[tone]}`} aria-hidden="true" />
            <span className="stat-label">{c.label}</span>
            <strong
              className={
                tone === "danger"
                  ? "stat-value neg"
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
