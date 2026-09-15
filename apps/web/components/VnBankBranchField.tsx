"use client";

import { useId, useMemo, useState } from "react";
import { suggestBankBranches } from "@/lib/vn-admin";

type Props = {
  name?: string;
  disabled?: boolean;
  bankShortName?: string | null;
  initialValue?: string;
};

export function VnBankBranchField({
  name = "bankBranch",
  disabled,
  bankShortName,
  initialValue = "",
}: Props) {
  const listId = useId();
  const inputId = useId();
  const [value, setValue] = useState(initialValue);
  const suggestions = useMemo(
    () => suggestBankBranches(bankShortName),
    [bankShortName]
  );

  return (
    <div className="field">
      <label htmlFor={inputId}>Chi nhánh</label>
      <input
        id={inputId}
        name={name}
        list={listId}
        value={value}
        disabled={disabled}
        placeholder="Chọn hoặc nhập chi nhánh…"
        autoComplete="off"
        onChange={(e) => setValue(e.target.value)}
      />
      <datalist id={listId}>
        {suggestions.map((s) => (
          <option key={s} value={s} />
        ))}
      </datalist>
      <p className="muted small">
        Gợi ý theo tỉnh/TP sau sáp nhập — có thể nhập tên chi nhánh cụ thể nếu
        không có trong danh sách (không có API chi nhánh công khai đầy đủ).
      </p>
    </div>
  );
}
