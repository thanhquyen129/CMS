"use client";

import { useEffect, useState } from "react";

type CurrencyRow = { code?: string; isActive?: boolean };

type Props = {
  name?: string;
  id: string;
  label?: string;
  defaultValue?: string;
  value?: string;
  onChange?: (code: string) => void;
  disabled?: boolean;
  required?: boolean;
};

export function CurrencySelect({
  name = "currencyCode",
  id,
  label = "Tiền tệ",
  defaultValue = "VND",
  value: valueProp,
  onChange,
  disabled,
  required = true,
}: Props) {
  const [codes, setCodes] = useState<string[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [value, setValue] = useState(defaultValue.toUpperCase());

  useEffect(() => {
    let cancelled = false;
    void fetch("/bff/admin/currencies?activeOnly=true", { cache: "no-store" })
      .then(async (res) => {
        if (!res.ok) throw new Error("fail");
        return (await res.json()) as CurrencyRow[];
      })
      .then((rows) => {
        if (cancelled) return;
        const list = (Array.isArray(rows) ? rows : [])
          .filter((r) => r.isActive !== false && r.code)
          .map((r) => r.code!.toUpperCase());
        const unique = [...new Set(list)];
        setCodes(unique);
        setValue((current) =>
          unique.includes(current)
            ? current
            : unique.includes("VND")
              ? "VND"
              : unique[0] ?? current
        );
        if (unique.length === 0) {
          setError("Danh mục tiền tệ trống. Khai báo tiền tệ trước khi ghi nhận.");
        }
      })
      .catch(() => {
        if (!cancelled) setError("Không tải được danh mục tiền tệ.");
      });
    return () => {
      cancelled = true;
    };
  }, []);

  return (
    <div className="field">
      <label htmlFor={id}>{label}</label>
      <select
        id={id}
        name={name}
        required={required}
        disabled={disabled || codes.length === 0}
        value={valueProp ?? value}
        onChange={(e) => {
          setValue(e.target.value);
          onChange?.(e.target.value);
        }}
      >
        {codes.length === 0 ? <option value="">—</option> : null}
        {codes.map((code) => (
          <option key={code} value={code}>
            {code}
          </option>
        ))}
      </select>
      {error ? <p className="muted small">{error}</p> : null}
    </div>
  );
}
