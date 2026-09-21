"use client";

import { downloadCsv } from "@/lib/list-workspace";

type Props = {
  filename: string;
  headers: string[];
  rows: Array<Array<string | number | null | undefined>>;
  label?: string;
};

/** Downloads the already-loaded/filtered rows — not a fake control. */
export function ExportCsvButton({ filename, headers, rows, label = "Xuất Excel" }: Props) {
  return (
    <button
      type="button"
      className="btn btn-ghost btn-sm"
      disabled={rows.length === 0}
      onClick={() => downloadCsv(filename, headers, rows)}
    >
      {label}
    </button>
  );
}
