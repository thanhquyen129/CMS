"use client";

import { useEffect, useId, useMemo, useRef, useState } from "react";
import {
  lookupPartiesClient,
  type PartyLookupItem,
} from "@/lib/parties-client";
import { creditStatusLabel, partyLabel, partyRoleLabel } from "@/lib/party";

type Props = {
  name: string;
  label: string;
  roleCode?: string;
  disabled?: boolean;
  required?: boolean;
  defaultId?: string | null;
  defaultLabel?: string | null;
  hint?: string;
  usableOnly?: boolean;
  onSelect?: (item: PartyLookupItem | null) => void;
};

export function PartyTypeahead({
  name,
  label,
  roleCode,
  disabled,
  required,
  defaultId,
  defaultLabel,
  hint,
  usableOnly = true,
  onSelect,
}: Props) {
  const listId = useId();
  const rootRef = useRef<HTMLDivElement>(null);
  const [query, setQuery] = useState(defaultLabel ?? "");
  const [open, setOpen] = useState(false);
  const [items, setItems] = useState<PartyLookupItem[]>([]);
  const [selected, setSelected] = useState<PartyLookupItem | null>(null);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    function onDoc(e: MouseEvent) {
      if (!rootRef.current?.contains(e.target as Node)) setOpen(false);
    }
    document.addEventListener("mousedown", onDoc);
    return () => document.removeEventListener("mousedown", onDoc);
  }, []);

  useEffect(() => {
    const q = query.trim();
    if (!open) return;
    const handle = window.setTimeout(() => {
      setLoading(true);
      void lookupPartiesClient({
        q: q.length >= 1 ? q : undefined,
        roleCode,
        usableOnly,
        take: 12,
      })
        .then(setItems)
        .finally(() => setLoading(false));
    }, 220);
    return () => window.clearTimeout(handle);
  }, [query, open, roleCode, usableOnly]);

  const selectedId = selected?.id ?? defaultId ?? "";

  const warning = useMemo(() => {
    const status = selected?.creditStatus;
    if (status === "over" || status === "watch") {
      return selected?.creditMessage || creditStatusLabel(status);
    }
    return null;
  }, [selected]);

  function pick(item: PartyLookupItem) {
    setSelected(item);
    setQuery(partyLabel(item));
    setOpen(false);
    onSelect?.(item);
  }

  function clear() {
    setSelected(null);
    setQuery("");
    onSelect?.(null);
  }

  return (
    <div className="field party-typeahead" ref={rootRef}>
      <label htmlFor={listId}>{label}</label>
      <input type="hidden" name={name} value={selectedId} />
      <div className="party-typeahead-row">
        <input
          id={listId}
          type="search"
          value={query}
          disabled={disabled}
          required={required}
          autoComplete="off"
          placeholder="Mã, tên, MST, SĐT…"
          onFocus={() => setOpen(true)}
          onChange={(e) => {
            setQuery(e.target.value);
            setSelected(null);
            setOpen(true);
            onSelect?.(null);
          }}
        />
        {selectedId ? (
          <button
            type="button"
            className="btn btn-ghost btn-sm"
            disabled={disabled}
            onClick={clear}
          >
            Xóa
          </button>
        ) : null}
      </div>
      {open ? (
        <ul className="party-typeahead-list" role="listbox">
          {loading ? (
            <li className="muted">Đang tìm…</li>
          ) : items.length === 0 ? (
            <li className="muted">Không có đối tác khớp. Tạo mới tại Danh mục → Đối tác.</li>
          ) : (
            items.map((item) => (
              <li key={item.id}>
                <button type="button" onClick={() => pick(item)}>
                  <strong>{partyLabel(item)}</strong>
                  <span className="muted small">
                    {[
                      item.taxId ? `MST ${item.taxId}` : null,
                      item.phone,
                      (item.roleCodes ?? []).map(partyRoleLabel).join(", ") || null,
                    ]
                      .filter(Boolean)
                      .join(" · ")}
                  </span>
                </button>
              </li>
            ))
          )}
        </ul>
      ) : null}
      {hint ? <p className="muted small">{hint}</p> : null}
      {warning ? (
        <p className="alert alert-warning" role="status">
          {warning}
        </p>
      ) : null}
    </div>
  );
}
