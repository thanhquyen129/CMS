"use client";

import { useEffect, useId, useMemo, useRef, useState } from "react";
import {
  bankDisplayLabel,
  findVnBank,
  searchVnBanks,
  type VnBank,
} from "@/lib/vn-banks";

type Props = {
  name?: string;
  disabled?: boolean;
  required?: boolean;
  initialValue?: string;
  onChange?: (bank: VnBank | null, displayName: string) => void;
};

export function VnBankAutocomplete({
  name = "bankName",
  disabled,
  required,
  initialValue = "",
  onChange,
}: Props) {
  const listId = useId();
  const rootRef = useRef<HTMLDivElement>(null);
  const [query, setQuery] = useState(initialValue);
  const [open, setOpen] = useState(false);
  const [selected, setSelected] = useState<VnBank | null>(
    () => findVnBank(initialValue) ?? null
  );

  const results = useMemo(() => searchVnBanks(query, 14), [query]);

  useEffect(() => {
    function onDoc(e: MouseEvent) {
      if (!rootRef.current?.contains(e.target as Node)) setOpen(false);
    }
    document.addEventListener("mousedown", onDoc);
    return () => document.removeEventListener("mousedown", onDoc);
  }, []);

  function pick(bank: VnBank) {
    setSelected(bank);
    setQuery(bank.shortName);
    setOpen(false);
    onChange?.(bank, bank.shortName);
  }

  function onInput(value: string) {
    setQuery(value);
    setOpen(true);
    const match = findVnBank(value);
    setSelected(match ?? null);
    onChange?.(match ?? null, value);
  }

  return (
    <div className="vn-bank-ac" ref={rootRef}>
      <input
        type="text"
        name={name}
        value={query}
        required={required}
        disabled={disabled}
        autoComplete="off"
        role="combobox"
        aria-expanded={open}
        aria-controls={listId}
        aria-autocomplete="list"
        placeholder="Gõ mã, tên viết tắt hoặc tên đầy đủ…"
        onFocus={() => setOpen(true)}
        onChange={(e) => onInput(e.target.value)}
      />
      {selected ? (
        <div className="vn-bank-ac-selected" aria-live="polite">
          {/* eslint-disable-next-line @next/next/no-img-element */}
          <img src={selected.logo} alt="" width={28} height={28} />
          <span>
            <strong>{selected.shortName}</strong>
            <span className="muted"> — {selected.name}</span>
          </span>
        </div>
      ) : null}
      {open ? (
        <ul id={listId} className="vn-bank-ac-list" role="listbox">
          {results.length === 0 ? (
            <li className="vn-bank-ac-empty">Không khớp ngân hàng trong danh mục VietQR.</li>
          ) : (
            results.map((b) => (
              <li key={b.bin}>
                <button
                  type="button"
                  role="option"
                  className="vn-bank-ac-option"
                  onClick={() => pick(b)}
                >
                  {/* eslint-disable-next-line @next/next/no-img-element */}
                  <img src={b.logo} alt="" width={28} height={28} />
                  <span className="vn-bank-ac-text">
                    <strong>{b.shortName}</strong>
                    <span className="muted">{bankDisplayLabel(b).split(" — ")[1]}</span>
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
