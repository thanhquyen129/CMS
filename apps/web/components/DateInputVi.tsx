"use client";

import { useEffect, useRef, useState, type ChangeEvent } from "react";
import { formatDateOnlyVi } from "@/lib/money";

type Props = {
  id?: string;
  name?: string;
  defaultValue?: string | null;
  value?: string | null;
  onChange?: (isoDate: string) => void;
  disabled?: boolean;
  required?: boolean;
  placeholder?: string;
  className?: string;
  style?: React.CSSProperties;
};

/** Converts ISO YYYY-MM-DD to DD/MM/YYYY */
function toDisplay(iso: string | null | undefined): string {
  if (!iso) return "";
  const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(iso);
  if (match) return `${match[3]}/${match[2]}/${match[1]}`;
  return formatDateOnlyVi(iso);
}

/** Converts DD/MM/YYYY to ISO YYYY-MM-DD */
function toIso(display: string): string {
  const match = /^(\d{1,2})\/(\d{1,2})\/(\d{4})$/.exec(display.trim());
  if (!match) return "";
  const d = match[1].padStart(2, "0");
  const m = match[2].padStart(2, "0");
  const y = match[3];
  return `${y}-${m}-${d}`;
}

export function DateInputVi({
  id,
  name,
  defaultValue,
  value,
  onChange,
  disabled = false,
  required = false,
  placeholder = "dd/mm/yyyy",
  className = "",
  style,
}: Props) {
  const initialIso = (value !== undefined ? value : defaultValue) ?? "";
  const [isoVal, setIsoVal] = useState<string>(initialIso ? initialIso.slice(0, 10) : "");
  const [displayVal, setDisplayVal] = useState<string>(toDisplay(initialIso));
  const datePickerRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (value !== undefined) {
      const cleanIso = value ? value.slice(0, 10) : "";
      setIsoVal(cleanIso);
      setDisplayVal(toDisplay(cleanIso));
    }
  }, [value]);

  function handleTextChange(e: ChangeEvent<HTMLInputElement>) {
    const text = e.target.value;
    setDisplayVal(text);
    const parsedIso = toIso(text);
    if (parsedIso) {
      setIsoVal(parsedIso);
      onChange?.(parsedIso);
    } else if (!text.trim()) {
      setIsoVal("");
      onChange?.("");
    }
  }

  function handlePickerChange(e: ChangeEvent<HTMLInputElement>) {
    const pickedIso = e.target.value;
    setIsoVal(pickedIso);
    setDisplayVal(toDisplay(pickedIso));
    onChange?.(pickedIso);
  }

  function openPicker() {
    if (disabled) return;
    const el = datePickerRef.current;
    if (!el) return;
    try {
      if ("showPicker" in el && typeof (el as unknown as { showPicker: () => void }).showPicker === "function") {
        (el as unknown as { showPicker: () => void }).showPicker();
      } else {
        el.focus();
      }
    } catch {
      el.focus();
    }
  }

  return (
    <div
      style={{
        display: "inline-flex",
        alignItems: "center",
        position: "relative",
        width: "100%",
        maxWidth: "240px",
        ...style,
      }}
      className={`date-input-vi-wrap ${className}`}
    >
      {/* Hidden input carrying the exact ISO YYYY-MM-DD name for native form submission */}
      {name ? <input type="hidden" name={name} value={isoVal} /> : null}

      {/* Visible formatted text box showing strictly DD/MM/YYYY */}
      <input
        id={id}
        type="text"
        inputMode="numeric"
        placeholder={placeholder}
        value={displayVal}
        onChange={handleTextChange}
        disabled={disabled}
        required={required}
        style={{
          width: "100%",
          paddingRight: "36px",
          fontFamily: "monospace, sans-serif",
          fontSize: "0.95rem",
        }}
      />

      {/* Calendar picker button icon */}
      <button
        type="button"
        onClick={openPicker}
        disabled={disabled}
        title="Chọn ngày trên lịch"
        aria-label="Chọn ngày trên lịch"
        style={{
          position: "absolute",
          right: "4px",
          top: "50%",
          transform: "translateY(-50%)",
          background: "transparent",
          border: "none",
          cursor: disabled ? "default" : "pointer",
          padding: "4px 6px",
          color: "#4b5563",
          display: "flex",
          alignItems: "center",
        }}
      >
        📅
      </button>

      {/* Hidden native HTML5 date input used for calendar popup */}
      <input
        ref={datePickerRef}
        type="date"
        tabIndex={-1}
        value={isoVal}
        onChange={handlePickerChange}
        disabled={disabled}
        style={{
          position: "absolute",
          opacity: 0,
          pointerEvents: "none",
          width: "1px",
          height: "1px",
          bottom: 0,
          right: "10px",
        }}
      />
    </div>
  );
}
