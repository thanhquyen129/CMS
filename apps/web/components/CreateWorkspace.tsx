import type { ReactNode } from "react";
import Link from "next/link";

export type CreateStep = { id: string; label: string };

/** UI-02 create chrome: breadcrumb, title, step rail, form + summary. */
export function CreateWorkspace({
  breadcrumbs,
  title,
  lede,
  actions,
  steps,
  currentStep,
  error,
  summary,
  children,
}: {
  breadcrumbs: { href?: string; label: string }[];
  title: string;
  lede: ReactNode;
  actions: ReactNode;
  steps: CreateStep[];
  currentStep: string;
  error?: string | null;
  summary: ReactNode;
  children: ReactNode;
}) {
  return (
    <div className="create-workspace">
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
          <p className="lede">{lede}</p>
        </div>
        <div className="create-footer-actions">{actions}</div>
      </div>
      {error ? (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      ) : null}
      <ol className="create-steps" aria-label="Các bước">
        {steps.map((s, i) => (
          <li
            key={s.id}
            className={`create-step${s.id === currentStep ? " is-on" : ""}`}
          >
            <div className="create-step-dot">{i + 1}</div>
            {s.label}
          </li>
        ))}
      </ol>
      <div className="create-layout">
        <div className="create-form-card">{children}</div>
        <aside className="create-summary">{summary}</aside>
      </div>
    </div>
  );
}

export function CreateSection({
  title,
  hint,
  badge,
  children,
}: {
  title: string;
  hint?: string;
  badge?: ReactNode;
  children: ReactNode;
}) {
  return (
    <section className="create-section">
      <div className="create-section-title">
        <div>
          <h2>{title}</h2>
          {hint ? <p>{hint}</p> : null}
        </div>
        {badge}
      </div>
      {children}
    </section>
  );
}
