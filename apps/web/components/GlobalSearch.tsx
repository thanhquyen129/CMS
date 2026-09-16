"use client";

import { useRouter } from "next/navigation";
import { FormEvent, useEffect, useRef, useState } from "react";

/**
 * Global search (PO top bar). MVP: tìm Bill theo số hiệu — không fabricate
 * multi-entity search khi chưa có API thống nhất.
 */
export function GlobalSearch() {
  const router = useRouter();
  const inputRef = useRef<HTMLInputElement>(null);
  const [q, setQ] = useState("");

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

  function onSubmit(e: FormEvent) {
    e.preventDefault();
    const value = q.trim();
    const href = value
      ? `/bills?q=${encodeURIComponent(value)}`
      : "/bills";
    router.push(href);
  }

  return (
    <form className="global-search" role="search" onSubmit={onSubmit}>
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
        placeholder="Tìm kiếm (bill, chứng từ, khách hàng, nhà cung cấp…)"
        autoComplete="off"
      />
      <kbd className="global-search-kbd" aria-hidden="true">
        Ctrl&nbsp;K
      </kbd>
      <button className="sr-only" type="submit">
        Tìm
      </button>
    </form>
  );
}
