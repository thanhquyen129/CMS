"use client";

import { useEffect, useId, useRef, useState } from "react";

type BillHit = {
  id: string;
  billNo: string;
  customerName?: string | null;
  externalId?: string | null;
};

type Props = {
  name?: string;
  label: string;
  disabled?: boolean;
  defaultId?: string | null;
  defaultLabel?: string | null;
};

export function BillTypeahead({
  name = "billId",
  label,
  disabled,
  defaultId,
  defaultLabel,
}: Props) {
  const listId = useId();
  const rootRef = useRef<HTMLDivElement>(null);
  const [query, setQuery] = useState(defaultLabel ?? "");
  const [open, setOpen] = useState(false);
  const [items, setItems] = useState<BillHit[]>([]);
  const [selectedId, setSelectedId] = useState(defaultId ?? "");
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    function onDoc(e: MouseEvent) {
      if (!rootRef.current?.contains(e.target as Node)) setOpen(false);
    }
    document.addEventListener("mousedown", onDoc);
    return () => document.removeEventListener("mousedown", onDoc);
  }, []);

  useEffect(() => {
    if (!open) return;
    const q = query.trim();
    const handle = window.setTimeout(() => {
      setLoading(true);
      const params = new URLSearchParams();
      if (q) params.set("q", q);
      params.set("page", "1");
      params.set("pageSize", "12");
      void fetch(`/bff/bills?${params.toString()}`, { cache: "no-store" })
        .then(async (res) => {
          if (!res.ok) return [] as BillHit[];
          const body = (await res.json()) as BillHit[] | { items?: BillHit[] };
          return Array.isArray(body) ? body : body.items ?? [];
        })
        .then(setItems)
        .finally(() => setLoading(false));
    }, 220);
    return () => window.clearTimeout(handle);
  }, [query, open]);

  function pick(item: BillHit) {
    setSelectedId(item.id);
    setQuery(item.billNo);
    setOpen(false);
  }

  function clear() {
    setSelectedId("");
    setQuery("");
    setItems([]);
  }

  return (
    <div className="field party-typeahead" ref={rootRef}>
      <label htmlFor={listId}>{label}</label>
      <input type="hidden" name={name} value={selectedId || query.trim()} />
      <div className="party-typeahead-row">
        <input
          id={listId}
          type="search"
          value={query}
          disabled={disabled}
          autoComplete="off"
          placeholder="Số Bill, mã ngoài, khách hàng…"
          onFocus={() => setOpen(true)}
          onChange={(e) => {
            setQuery(e.target.value);
            setSelectedId("");
            setOpen(true);
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
          {loading ? <li className="muted small">Đang tìm…</li> : null}
          {!loading && items.length === 0 ? (
            <li className="muted small">Không có Bill khớp.</li>
          ) : null}
          {items.map((item) => (
            <li key={item.id}>
              <button type="button" onClick={() => pick(item)}>
                <strong>{item.billNo}</strong>
                {item.customerName ? ` · ${item.customerName}` : ""}
              </button>
            </li>
          ))}
        </ul>
      ) : null}
    </div>
  );
}
