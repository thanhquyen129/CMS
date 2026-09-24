"use client";

import { useEffect, useState } from "react";

type Item = { code?: string; name?: string; isActive?: boolean };

type Props = {
  id: string;
  name?: string;
  kind: "cost_type" | "revenue_type";
  label: string;
  disabled?: boolean;
  required?: boolean;
  defaultValue?: string | null;
};

export function CatalogCodeSelect({
  id,
  name = "typeCode",
  kind,
  label,
  disabled,
  required,
  defaultValue,
}: Props) {
  const [items, setItems] = useState<Item[]>([]);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const params = new URLSearchParams({ kind, activeOnly: "true" });
    void fetch(`/bff/admin/catalog?${params.toString()}`, { cache: "no-store" })
      .then(async (res) => {
        if (!res.ok) throw new Error("fail");
        return (await res.json()) as Item[];
      })
      .then((rows) => {
        const list = (Array.isArray(rows) ? rows : []).filter(
          (r) => r.isActive !== false && r.code
        );
        setItems(list);
        if (list.length === 0) {
          setError("Chưa có mã trong danh mục. Khai báo trước khi chọn.");
        }
      })
      .catch(() => setError("Không tải được danh mục."));
  }, [kind]);

  return (
    <div className="field">
      <label htmlFor={id}>{label}</label>
      <select
        id={id}
        name={name}
        disabled={disabled || items.length === 0}
        required={required}
        defaultValue={defaultValue ?? ""}
      >
        <option value="">{required ? "Chọn" : "—"}</option>
        {items.map((item) => (
          <option key={item.code} value={item.code}>
            {item.code}
            {item.name ? ` — ${item.name}` : ""}
          </option>
        ))}
      </select>
      {error ? <p className="muted small">{error}</p> : null}
    </div>
  );
}
