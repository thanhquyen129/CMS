"use client";

import type { CatalogOption } from "@/lib/create-workspace";

/** Location picker: catalog select when master data exists, otherwise free text. */
export function LocationField({
  id,
  name,
  label,
  required,
  value,
  onChange,
  options,
  disabled,
}: {
  id: string;
  name: string;
  label: string;
  required?: boolean;
  value: string;
  onChange: (v: string) => void;
  options: CatalogOption[];
  disabled?: boolean;
}) {
  return (
    <div className="cw-field">
      <label htmlFor={id}>
        {label}
        {required ? <span className="req"> *</span> : null}
      </label>
      {options.length > 0 ? (
        <select
          id={id}
          name={name}
          required={required}
          value={value}
          onChange={(e) => onChange(e.target.value)}
          disabled={disabled}
        >
          <option value="">Chọn cảng / sân bay / địa điểm</option>
          {options.map((l) => (
            <option key={`${id}-${l.value}`} value={l.value}>
              {l.label}
            </option>
          ))}
        </select>
      ) : (
        <input
          id={id}
          name={name}
          required={required}
          value={value}
          onChange={(e) => onChange(e.target.value)}
          disabled={disabled}
          placeholder="Nhập mã điểm (vd. SGN, LAX)"
          maxLength={64}
        />
      )}
    </div>
  );
}
