"use client";

import type { ChangeEvent, FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { newIdempotencyKey, withIdempotency } from "@/lib/idempotency";

const CSV_HEADER =
  "objectType,businessNo,externalId,originCode,destinationCode,grossWeightKg,chargeableWeightKg,parentExternalId,sequenceNo,movementOn";

type Issue = { row: number; field: string; message: string };
type Preview = { canCommit: boolean; issues: Issue[] };

type Row = {
  objectType: string;
  businessNo: string;
  externalId: string;
  originCode?: string | null;
  destinationCode?: string | null;
  grossWeightKg?: number | null;
  chargeableWeightKg?: number | null;
  parentExternalId?: string | null;
  sequenceNo?: number | null;
  movementOn?: string | null;
};

function parseCsv(text: string): { rows: Row[]; parseErrors: string[] } {
  const lines = text
    .replace(/^\uFEFF/, "")
    .split(/\r?\n/)
    .map((l) => l.trim())
    .filter((l) => l.length > 0);
  if (lines.length === 0) return { rows: [], parseErrors: ["File trống."] };

  const header = lines[0].split(",").map((h) => h.trim().toLowerCase());
  const idx = (name: string) => header.indexOf(name.toLowerCase());
  const iType = idx("objectType");
  const iNo = idx("businessNo");
  const iExt = idx("externalId");
  if (iType < 0 || iNo < 0 || iExt < 0) {
    return {
      rows: [],
      parseErrors: [
        `Thiếu cột bắt buộc objectType, businessNo, externalId. Header chuẩn: ${CSV_HEADER}`,
      ],
    };
  }

  const rows: Row[] = [];
  const parseErrors: string[] = [];
  for (let li = 1; li < lines.length; li++) {
    const cols = lines[li].split(",").map((c) => c.trim());
    const objectType = (cols[iType] ?? "").toLowerCase();
    const businessNo = cols[iNo] ?? "";
    const externalId = cols[iExt] ?? "";
    if (!objectType && !businessNo && !externalId) continue;

    const num = (name: string): number | null => {
      const i = idx(name);
      if (i < 0 || !cols[i]) return null;
      const n = Number(cols[i]);
      return Number.isFinite(n) ? n : null;
    };
    const str = (name: string): string | null => {
      const i = idx(name);
      if (i < 0 || !cols[i]) return null;
      return cols[i];
    };

    rows.push({
      objectType,
      businessNo,
      externalId,
      originCode: str("originCode"),
      destinationCode: str("destinationCode"),
      grossWeightKg: num("grossWeightKg"),
      chargeableWeightKg: num("chargeableWeightKg"),
      parentExternalId: str("parentExternalId"),
      sequenceNo: num("sequenceNo") != null ? Math.trunc(num("sequenceNo")!) : null,
      movementOn: str("movementOn"),
    });
  }

  if (rows.length === 0 && parseErrors.length === 0) {
    parseErrors.push("Không có dòng dữ liệu sau header.");
  }
  return { rows, parseErrors };
}

export function ImportOperationalForm() {
  const router = useRouter();
  const [sourceSystem, setSourceSystem] = useState("import_csv");
  const [csv, setCsv] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [preview, setPreview] = useState<Preview | null>(null);
  const [committed, setCommitted] = useState<number | null>(null);
  const [busy, setBusy] = useState(false);
  const [isPending, startTransition] = useTransition();

  function onFileChange(e: ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    if (!file) return;
    const reader = new FileReader();
    reader.onload = () => {
      setCsv(String(reader.result ?? ""));
      setPreview(null);
      setCommitted(null);
      setError(null);
    };
    reader.readAsText(file);
    e.target.value = "";
  }

  async function run(mode: "preview" | "commit") {
    setError(null);
    setCommitted(null);
    const { rows, parseErrors } = parseCsv(csv);
    if (parseErrors.length) {
      setError(parseErrors.join(" "));
      setPreview(null);
      return;
    }
    if (!sourceSystem.trim()) {
      setError("Nhập mã hệ thống nguồn (sourceSystem).");
      return;
    }

    setBusy(true);
    try {
      const path =
        mode === "preview"
          ? "/bff/operational-import/preview"
          : "/bff/operational-import/commit";
      const headers =
        mode === "commit"
          ? withIdempotency(
              { "Content-Type": "application/json" },
              newIdempotencyKey("ops-import")
            )
          : { "Content-Type": "application/json", Accept: "application/json" };

      const res = await fetch(path, {
        method: "POST",
        headers,
        body: JSON.stringify({ sourceSystem: sourceSystem.trim(), rows }),
      });

      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }

      const payload = (await res.json().catch(() => ({}))) as Preview & {
        committed?: number;
        message?: string;
      };

      if (!res.ok) {
        setError(
          payload.message ||
            (res.status === 409
              ? "Không ghi được — còn lỗi trên dòng. Xem bảng kiểm tra."
              : "Thao tác nhập thất bại.")
        );
        if (payload.issues) setPreview({ canCommit: false, issues: payload.issues });
        return;
      }

      if (mode === "preview") {
        setPreview({
          canCommit: Boolean(payload.canCommit),
          issues: payload.issues ?? [],
        });
      } else {
        setCommitted(payload.committed ?? rows.length);
        setPreview({ canCommit: true, issues: [] });
        startTransition(() => router.refresh());
      }
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      setBusy(false);
    }
  }

  async function onPreview(e: FormEvent) {
    e.preventDefault();
    await run("preview");
  }

  const loading = busy || isPending;

  return (
    <form className="receive-form" onSubmit={onPreview}>
      <p className="note">
        Nhập hàng loạt Order / Bill / Shipment / Chặng / Chuyến. Xem trước từng dòng;
        một dòng lỗi thì không ghi gì. Header:{" "}
        <code className="mono-id">{CSV_HEADER}</code>.{" "}
        <code>objectType</code>: bill | order | shipment | leg | movement.
      </p>

      <div className="form-grid">
        <div className="field">
          <label htmlFor="ops-src">Hệ thống nguồn</label>
          <input
            id="ops-src"
            value={sourceSystem}
            onChange={(e) => setSourceSystem(e.target.value)}
            disabled={loading}
            required
          />
        </div>
        <div className="field field-span">
          <label htmlFor="ops-file">Chọn file CSV</label>
          <input
            id="ops-file"
            type="file"
            accept=".csv,text/csv"
            onChange={onFileChange}
            disabled={loading}
          />
        </div>
        <div className="field field-span">
          <label htmlFor="ops-csv">Nội dung CSV</label>
          <textarea
            id="ops-csv"
            rows={10}
            value={csv}
            onChange={(e) => {
              setCsv(e.target.value);
              setPreview(null);
              setCommitted(null);
            }}
            disabled={loading}
            placeholder={CSV_HEADER}
          />
        </div>
      </div>

      {error ? (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      ) : null}

      {committed != null ? (
        <div className="alert alert-success" role="status">
          Đã ghi {committed} dòng. Kiểm tra danh sách Bill / đơn hàng / Shipment.
        </div>
      ) : null}

      {preview ? (
        <div className="table-wrap" style={{ marginTop: "1rem" }}>
          <p className="muted">
            {preview.canCommit
              ? "Kiểm tra đạt — có thể ghi."
              : `Còn ${preview.issues.length} lỗi — chưa ghi.`}
          </p>
          {preview.issues.length > 0 ? (
            <table className="data-table">
              <thead>
                <tr>
                  <th>Dòng</th>
                  <th>Trường</th>
                  <th>Lỗi</th>
                </tr>
              </thead>
              <tbody>
                {preview.issues.map((iss, i) => (
                  <tr key={`${iss.row}-${iss.field}-${i}`}>
                    <td>{iss.row}</td>
                    <td>
                      <code>{iss.field}</code>
                    </td>
                    <td>{iss.message}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          ) : null}
        </div>
      ) : null}

      <div className="cta-row" style={{ marginTop: "1rem" }}>
        <button type="submit" className="btn" disabled={loading || !csv.trim()}>
          {loading ? "Đang kiểm tra…" : "Xem trước"}
        </button>
        <button
          type="button"
          className="btn btn-primary"
          disabled={loading || !preview?.canCommit}
          onClick={() => void run("commit")}
        >
          {loading ? "Đang ghi…" : "Ghi tất cả"}
        </button>
      </div>
    </form>
  );
}
