"use client";

import { useEffect, useId, useState } from "react";

export type RefHit = { id: string; code: string; title?: string };

/** Search + tag picker for Order/Bill/Shipment links. Uses real /bff/search. */
export function RefTagPicker({
  entityType,
  label,
  placeholder,
  hint,
  selected,
  onChange,
  initialHits,
}: {
  entityType: "order" | "bill" | "shipment";
  label: string;
  placeholder: string;
  hint?: string;
  selected: RefHit[];
  onChange: (next: RefHit[]) => void;
  initialHits?: RefHit[];
}) {
  const inputId = useId();
  const [query, setQuery] = useState("");
  const [open, setOpen] = useState(false);
  const [hits, setHits] = useState<RefHit[]>(initialHits ?? []);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    const q = query.trim();
    if (!open) return;
    if (q.length < 1) {
      setHits(initialHits ?? []);
      return;
    }
    const handle = window.setTimeout(() => {
      setLoading(true);
      void fetch(`/bff/search?q=${encodeURIComponent(q)}`, {
        headers: { Accept: "application/json" },
      })
        .then(async (res) => {
          if (res.status === 401) {
            window.location.href = "/login";
            return [];
          }
          if (!res.ok) return [];
          const rows = (await res.json()) as {
            entityType?: string;
            id?: string;
            code?: string;
            title?: string;
          }[];
          return rows
            .filter((r) => r.entityType === entityType && r.id && r.code)
            .map((r) => ({ id: r.id!, code: r.code!, title: r.title }));
        })
        .then(setHits)
        .finally(() => setLoading(false));
    }, 220);
    return () => window.clearTimeout(handle);
  }, [query, open, entityType, initialHits]);

  function add(hit: RefHit) {
    if (selected.some((s) => s.id === hit.id)) return;
    onChange([...selected, hit]);
    setQuery("");
    setOpen(false);
  }

  function remove(id: string) {
    onChange(selected.filter((s) => s.id !== id));
  }

  return (
    <div className="cw-field s6 ref-picker">
      <label htmlFor={inputId}>{label}</label>
      <input
        id={inputId}
        type="search"
        value={query}
        placeholder={placeholder}
        autoComplete="off"
        onFocus={() => setOpen(true)}
        onChange={(e) => {
          setQuery(e.target.value);
          setOpen(true);
        }}
        onBlur={() => window.setTimeout(() => setOpen(false), 180)}
      />
      {open ? (
        <ul className="ref-picker-list" role="listbox">
          {loading ? (
            <li className="muted">Đang tìm…</li>
          ) : hits.length === 0 ? (
            <li className="muted">Không có kết quả.</li>
          ) : (
            hits.map((h) => (
              <li key={h.id}>
                <button type="button" onMouseDown={(e) => e.preventDefault()} onClick={() => add(h)}>
                  <strong>{h.code}</strong>
                  {h.title ? <span className="muted small"> {h.title}</span> : null}
                </button>
              </li>
            ))
          )}
        </ul>
      ) : null}
      {selected.length > 0 ? (
        <div className="ref-tags">
          {selected.map((s) => (
            <span key={s.id} className="create-tag">
              {s.code}
              <button type="button" aria-label={`Bỏ ${s.code}`} onClick={() => remove(s.id)}>
                ×
              </button>
            </span>
          ))}
        </div>
      ) : null}
      {hint ? <p className="cw-hint">{hint}</p> : null}
    </div>
  );
}
