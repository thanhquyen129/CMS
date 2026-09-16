import type { ReactNode } from "react";
import Link from "next/link";

export type FilterField =
  | {
      kind: "search";
      name: string;
      id?: string;
      label: string;
      placeholder?: string;
      defaultValue?: string;
    }
  | {
      kind: "select";
      name: string;
      id?: string;
      label: string;
      defaultValue?: string;
      options: { value: string; label: string }[];
      emptyLabel?: string;
    }
  | {
      kind: "date";
      name: string;
      id?: string;
      label: string;
      defaultValue?: string;
    };

/** GET form filter bar — only wire fields the API supports. */
export function FilterBar({
  action,
  fields,
  hidden,
  submitLabel = "Lọc",
  resetHref,
  extra,
}: {
  action: string;
  fields: FilterField[];
  hidden?: Record<string, string | undefined>;
  submitLabel?: string;
  resetHref?: string;
  extra?: ReactNode;
}) {
  return (
    <form className="search-bar denser-filters" method="get" action={action} role="search">
      {hidden
        ? Object.entries(hidden).map(([k, v]) =>
            v != null && v !== "" ? (
              <input key={k} type="hidden" name={k} value={v} />
            ) : null
          )
        : null}
      {fields.map((f) => {
        const id = f.id ?? f.name;
        if (f.kind === "search") {
          return (
            <span key={f.name} className="filter-field">
              <label className="sr-only" htmlFor={id}>
                {f.label}
              </label>
              <input
                id={id}
                name={f.name}
                type="search"
                placeholder={f.placeholder ?? f.label}
                defaultValue={f.defaultValue ?? ""}
                autoComplete="off"
              />
            </span>
          );
        }
        if (f.kind === "date") {
          return (
            <span key={f.name} className="filter-field">
              <label className="sr-only" htmlFor={id}>
                {f.label}
              </label>
              <input
                id={id}
                name={f.name}
                type="date"
                defaultValue={f.defaultValue ?? ""}
                aria-label={f.label}
              />
            </span>
          );
        }
        return (
          <span key={f.name} className="filter-field">
            <label className="sr-only" htmlFor={id}>
              {f.label}
            </label>
            <select id={id} name={f.name} defaultValue={f.defaultValue ?? ""}>
              <option value="">{f.emptyLabel ?? `Tất cả ${f.label.toLowerCase()}`}</option>
              {f.options.map((o) => (
                <option key={o.value} value={o.value}>
                  {o.label}
                </option>
              ))}
            </select>
          </span>
        );
      })}
      <button className="btn" type="submit">
        {submitLabel}
      </button>
      {resetHref ? (
        <Link className="btn btn-ghost" href={resetHref}>
          Làm mới
        </Link>
      ) : null}
      {extra}
    </form>
  );
}
