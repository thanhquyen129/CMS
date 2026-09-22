"use client";

import type { ChangeEvent, FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { newIdempotencyKey, withIdempotency } from "@/lib/idempotency";

export const PARTY_CSV_HEADER =
  "code,name,taxId,roleCodes,isCustomer,isVendor,isPayer,isPayee,legalName,phone,email,partyKind,countryCode,defaultCurrencyCode,paymentTermDays,creditLimit,groupCode,externalCode,shortName,notes";

type Issue = { row: number; field: string; message: string };
type Preview = { canCommit: boolean; issues: Issue[] };

type Row = {
  code: string;
  name: string;
  taxId?: string | null;
  roleCodes?: string[] | null;
  isCustomer?: boolean | null;
  isVendor?: boolean | null;
  isPayer?: boolean | null;
  isPayee?: boolean | null;
  legalName?: string | null;
  phone?: string | null;
  email?: string | null;
  partyKind?: string | null;
  countryCode?: string | null;
  defaultCurrencyCode?: string | null;
  paymentTermDays?: number | null;
  creditLimit?: number | null;
  groupCode?: string | null;
  externalCode?: string | null;
  shortName?: string | null;
  notes?: string | null;
};

function parseBool(raw: string | undefined): boolean | null {
  if (!raw) return null;
  const v = raw.trim().toLowerCase();
  if (["1", "true", "yes", "y", "x"].includes(v)) return true;
  if (["0", "false", "no", "n"].includes(v)) return false;
  return null;
}

function parseCsv(text: string): { rows: Row[]; parseErrors: string[] } {
  const lines = text
    .replace(/^\uFEFF/, "")
    .split(/\r?\n/)
    .map((l) => l.trim())
    .filter((l) => l.length > 0);
  if (lines.length === 0) return { rows: [], parseErrors: ["File trống."] };

  const header = lines[0].split(",").map((h) => h.trim().toLowerCase());
  const idx = (name: string) => header.indexOf(name.toLowerCase());
  const iCode = idx("code");
  const iName = idx("name");
  if (iCode < 0 || iName < 0) {
    return {
      rows: [],
      parseErrors: [
        `Thiếu cột bắt buộc code, name. Header chuẩn: ${PARTY_CSV_HEADER}`,
      ],
    };
  }

  const rows: Row[] = [];
  const parseErrors: string[] = [];
  for (let li = 1; li < lines.length; li++) {
    const cols = lines[li].split(",").map((c) => c.trim());
    const code = cols[iCode] ?? "";
    const name = cols[iName] ?? "";
    if (!code && !name) continue;

    const str = (name: string): string | null => {
      const i = idx(name);
      if (i < 0 || !cols[i]) return null;
      return cols[i];
    };
    const num = (name: string): number | null => {
      const i = idx(name);
      if (i < 0 || !cols[i]) return null;
      const n = Number(cols[i]);
      return Number.isFinite(n) ? n : null;
    };
    const flag = (name: string): boolean | null => {
      const i = idx(name);
      if (i < 0) return null;
      return parseBool(cols[i]);
    };

    const roleRaw = str("roleCodes");
    const roleCodes = roleRaw
      ? roleRaw
          .split(/[;|]/)
          .map((r) => r.trim().toLowerCase())
          .filter(Boolean)
      : null;

    rows.push({
      code,
      name,
      taxId: str("taxId"),
      roleCodes,
      isCustomer: flag("isCustomer"),
      isVendor: flag("isVendor"),
      isPayer: flag("isPayer"),
      isPayee: flag("isPayee"),
      legalName: str("legalName"),
      phone: str("phone"),
      email: str("email"),
      partyKind: str("partyKind"),
      countryCode: str("countryCode"),
      defaultCurrencyCode: str("defaultCurrencyCode"),
      paymentTermDays:
        num("paymentTermDays") != null
          ? Math.trunc(num("paymentTermDays")!)
          : null,
      creditLimit: num("creditLimit"),
      groupCode: str("groupCode"),
      externalCode: str("externalCode"),
      shortName: str("shortName"),
      notes: str("notes"),
    });
  }

  if (rows.length === 0 && parseErrors.length === 0) {
    parseErrors.push("Không có dòng dữ liệu sau header.");
  }
  return { rows, parseErrors };
}

export function ImportPartiesForm() {
  const router = useRouter();
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

    setBusy(true);
    try {
      const path =
        mode === "preview"
          ? "/bff/party-imports/preview"
          : "/bff/party-imports/commit";
      const headers =
        mode === "commit"
          ? withIdempotency(
              { "Content-Type": "application/json" },
              newIdempotencyKey("party-import")
            )
          : { "Content-Type": "application/json", Accept: "application/json" };

      const res = await fetch(path, {
        method: "POST",
        headers,
        body: JSON.stringify({ rows }),
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
        Nhập hàng loạt đối tác kinh doanh (CSV). Xem trước từng dòng; một dòng lỗi
        thì không ghi gì. Không gộp hồ sơ trùng MST/mã. Header:{" "}
        <code className="mono-id">{PARTY_CSV_HEADER}</code>. Vai trò: cột{" "}
        <code>roleCodes</code> (customer;vendor) hoặc cờ isCustomer / isVendor /
        isPayer / isPayee.
      </p>

      <div className="form-grid">
        <div className="field field-span">
          <label htmlFor="party-file">Chọn file CSV</label>
          <input
            id="party-file"
            type="file"
            accept=".csv,text/csv"
            onChange={onFileChange}
            disabled={loading}
          />
        </div>
        <div className="field field-span">
          <label htmlFor="party-csv">Nội dung CSV</label>
          <textarea
            id="party-csv"
            rows={10}
            value={csv}
            onChange={(e) => {
              setCsv(e.target.value);
              setPreview(null);
              setCommitted(null);
            }}
            disabled={loading}
            placeholder={PARTY_CSV_HEADER}
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
          Đã ghi {committed} đối tác. Kiểm tra danh sách khách hàng / nhà cung cấp.
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
