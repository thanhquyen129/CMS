"use client";

import { useRouter } from "next/navigation";
import { FormEvent, useEffect, useRef, useState } from "react";

type Hit = {
  entityType: string;
  id: string;
  code: string;
  title: string;
  matchKind: string;
};

const TYPE_LABEL: Record<string, string> = {
  bill: "Bill",
  order: "Đơn hàng",
  shipment: "Lô hàng",
  leg: "Chặng",
  movement: "Chuyến",
  cost: "Chi phí",
  revenue: "Doanh thu",
  document: "Chứng từ",
  party: "Đối tác",
};

function hrefFor(hit: Hit): string {
  switch (hit.entityType) {
    case "bill":
      return `/bills/${hit.id}`;
    case "order":
      return `/operations/orders/${hit.id}`;
    case "shipment":
      return `/operations/shipments/${hit.id}`;
    case "leg":
      return `/operations/legs/${hit.id}`;
    case "movement":
      return `/operations/movements/${hit.id}`;
    case "cost":
      return `/costs/${hit.id}`;
    case "revenue":
      return `/revenues/${hit.id}`;
    case "document":
      return `/documents/${hit.id}`;
    case "party":
      return `/admin/parties/${hit.id}`;
    default:
      return `/bills?q=${encodeURIComponent(hit.code)}`;
  }
}

export function GlobalSearch() {
  const router = useRouter();
  const inputRef = useRef<HTMLInputElement>(null);
  const boxRef = useRef<HTMLDivElement>(null);
  const [q, setQ] = useState("");
  const [hits, setHits] = useState<Hit[]>([]);
  const [open, setOpen] = useState(false);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === "k") {
        e.preventDefault();
        inputRef.current?.focus();
      }
    };
    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
  }, []);

  useEffect(() => {
    const value = q.trim();
    if (value.length < 2) {
      setHits([]);
      setOpen(false);
      return;
    }

    const handle = window.setTimeout(async () => {
      setLoading(true);
      try {
        const res = await fetch(`/bff/search?q=${encodeURIComponent(value)}`, {
          headers: { Accept: "application/json" },
        });
        if (!res.ok) {
          setHits([]);
          setOpen(false);
          return;
        }
        const data = (await res.json()) as Hit[];
        setHits(Array.isArray(data) ? data : []);
        setOpen(true);
      } catch {
        setHits([]);
      } finally {
        setLoading(false);
      }
    }, 250);

    return () => window.clearTimeout(handle);
  }, [q]);

  useEffect(() => {
    const onDoc = (e: MouseEvent) => {
      if (!boxRef.current?.contains(e.target as Node)) {
        setOpen(false);
      }
    };
    document.addEventListener("mousedown", onDoc);
    return () => document.removeEventListener("mousedown", onDoc);
  }, []);

  function go(hit: Hit) {
    setOpen(false);
    router.push(hrefFor(hit));
  }

  function onSubmit(e: FormEvent) {
    e.preventDefault();
    if (hits[0]) {
      go(hits[0]);
      return;
    }
    const value = q.trim();
    router.push(value ? `/bills?q=${encodeURIComponent(value)}` : "/bills");
  }

  return (
    <div className="global-search" ref={boxRef}>
      <form role="search" onSubmit={onSubmit}>
        <label className="sr-only" htmlFor="global-search-q">
          Tìm kiếm
        </label>
        <input
          ref={inputRef}
          id="global-search-q"
          type="search"
          name="q"
          value={q}
          onChange={(e) => setQ(e.target.value)}
          onFocus={() => hits.length > 0 && setOpen(true)}
          placeholder="⌕  Tìm kiếm (đơn hàng, bill, shipment, khách hàng, nhà cung cấp, chứng từ...)"
          autoComplete="off"
          aria-autocomplete="list"
          aria-expanded={open}
          aria-controls="global-search-results"
        />
        <kbd className="global-search-kbd" aria-hidden="true">
          Ctrl&nbsp;K
        </kbd>
        <button className="sr-only" type="submit">
          Tìm
        </button>
      </form>
      {open ? (
        <ul
          id="global-search-results"
          className="global-search-results"
          role="listbox"
        >
          {loading ? (
            <li className="muted small" role="status">
              Đang tìm…
            </li>
          ) : hits.length === 0 ? (
            <li className="muted small" role="status">
              Không có kết quả.
            </li>
          ) : (
            hits.map((h) => (
              <li key={`${h.entityType}-${h.id}-${h.matchKind}`} role="option">
                <button type="button" onClick={() => go(h)}>
                  <strong>{h.code}</strong>
                  <span>
                    {TYPE_LABEL[h.entityType] ?? h.entityType}
                    {h.title ? ` · ${h.title}` : ""}
                  </span>
                </button>
              </li>
            ))
          )}
        </ul>
      ) : null}
    </div>
  );
}
