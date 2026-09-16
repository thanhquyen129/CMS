import type { ReactNode } from "react";
import Link from "next/link";

export type BreadcrumbItem = { href?: string; label: string };

/** Page title row: breadcrumb + H1 + optional primary CTA. */
export function ListPageHeader({
  breadcrumbs,
  title,
  lede,
  action,
}: {
  breadcrumbs: BreadcrumbItem[];
  title: ReactNode;
  lede?: ReactNode;
  action?: ReactNode;
}) {
  return (
    <>
      <p className="breadcrumb">
        {breadcrumbs.map((b, i) => (
          <span key={`${b.label}-${i}`}>
            {i > 0 ? " / " : null}
            {b.href ? <Link href={b.href}>{b.label}</Link> : b.label}
          </span>
        ))}
      </p>
      <div className="page-header-row">
        <div>
          <h1>{title}</h1>
          {lede ? <p className="lede">{lede}</p> : null}
        </div>
        {action}
      </div>
    </>
  );
}
