"use client";

import type { ChangeEvent, FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useMemo, useState, useTransition } from "react";
import { newIdempotencyKey, withIdempotency } from "@/lib/idempotency";
import { partyRoleLabel } from "@/lib/party";
import { PARTY_CSV_SAMPLE, parsePartyCsv, type PartyCsvRow } from "@/lib/party-csv";

type Issue = { row: number; field: string; message: string };
type Preview = { canCommit: boolean; issues: Issue[] };

const ROLE_LABELS: Record<string, string> = {
  "khách hàng": "customer",
  "khach hang": "customer",
  kh: "customer",
  "nhà cung cấp": "vendor",
  "nha cung cap": "vendor",
  ncc: "vendor",
  "bên trả tiền": "payer",
  "ben tra tien": "payer",
  "bên nhận tiền": "payee",
  "ben nhan tien": "payee",
};

function roleText(row: PartyCsvRow): string {
  const codes = new Set<string>();
  for (const raw of row.roleCodes ?? []) {
    const key = raw.trim().toLowerCase();
    codes.add(ROLE_LABELS[key] ?? key);
  }
  if (row.isCustomer) codes.add("customer");
  if (row.isVendor) codes.add("vendor");
  if (row.isPayer) codes.add("payer");
  if (row.isPayee) codes.add("payee");
  if (codes.size === 0) return "—";
  return [...codes].map((code) => partyRoleLabel(code)).join(", ");
}

export function ImportPartiesForm() {
  const router = useRouter();
  const [csv, setCsv] = useState("");
  const [fileName, setFileName] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [preview, setPreview] = useState<Preview | null>(null);
  const [committed, setCommitted] = useState<number | null>(null);
  const [busy, setBusy] = useState(false);
  const [isPending, startTransition] = useTransition();

  const parsed = useMemo(() => (csv.trim() ? parsePartyCsv(csv) : null), [csv]);
  const issuesByRow = useMemo(() => {
    const map = new Map<number, string[]>();
    for (const issue of preview?.issues ?? []) {
      const list = map.get(issue.row) ?? [];
      list.push(issue.message);
      map.set(issue.row, list);
    }
    return map;
  }, [preview]);

  function onFileChange(e: ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    if (!file) return;
    setFileName(file.name);
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

  function downloadSample() {
    const blob = new Blob(["\uFEFF" + PARTY_CSV_SAMPLE], { type: "text/csv;charset=utf-8" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = "mau-nhap-doi-tac.csv";
    link.click();
    URL.revokeObjectURL(url);
  }

  async function run(mode: "preview" | "commit") {
    setError(null);
    setCommitted(null);
    const result = parsePartyCsv(csv);
    if (result.parseErrors.length) {
      setError(result.parseErrors.join(" "));
      setPreview(null);
      return;
    }

    setBusy(true);
    try {
      const path = mode === "preview" ? "/bff/party-imports/preview" : "/bff/party-imports/commit";
      const headers =
        mode === "commit"
          ? withIdempotency(
              { "Content-Type": "application/json", Accept: "application/json" },
              newIdempotencyKey("party-import")
            )
          : { "Content-Type": "application/json", Accept: "application/json" };

      const res = await fetch(path, {
        method: "POST",
        headers,
        body: JSON.stringify({ rows: result.rows }),
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
              ? "Không ghi được — còn lỗi trên dòng. Sửa file rồi xem trước lại."
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
        setCommitted(payload.committed ?? result.rows.length);
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
  const rows = parsed?.rows ?? [];

  return (
    <form className="receive-form" onSubmit={onPreview}>
      <p className="note">
        Mỗi dòng là một đối tác. Bắt buộc có Mã và Tên. Vai trò ghi tiếng Việt, cách nhau bởi dấu chấm phẩy
        (Khách hàng;Nhà cung cấp). Một dòng lỗi thì không ghi dòng nào. Trùng mã hoặc MST thì từ chối, không gộp hồ sơ.
        File xuất từ Excel dùng dấu phẩy hoặc chấm phẩy đều được. Tên có dấu phẩy phải đặt trong ngoặc kép.
      </p>

      <div className="page-header-actions" style={{ marginBottom: "1rem" }}>
        <label className="btn" htmlFor="party-file">
          Chọn file CSV
        </label>
        <input
          id="party-file"
          className="sr-only"
          type="file"
          accept=".csv,text/csv,text/plain"
          onChange={onFileChange}
          disabled={loading}
        />
        <button type="button" className="btn btn-ghost" disabled={loading} onClick={downloadSample}>
          Tải file mẫu
        </button>
        <span className="muted">{fileName ?? "Chưa chọn file. Có thể dán nội dung bên dưới."}</span>
      </div>

      <div className="field field-span">
        <label htmlFor="party-csv">Nội dung CSV</label>
        <textarea
          id="party-csv"
          rows={8}
          value={csv}
          onChange={(e) => {
            setCsv(e.target.value);
            setFileName(null);
            setPreview(null);
            setCommitted(null);
            setError(null);
          }}
          disabled={loading}
          placeholder="Dán nội dung CSV, hoặc bấm Chọn file CSV."
        />
      </div>

      {error ? (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      ) : null}

      {committed != null ? (
        <div className="alert alert-success" role="status">
          Đã ghi {committed} đối tác, gồm tài khoản và người liên hệ nếu file có. Kiểm tra danh sách khách hàng và nhà cung cấp.
        </div>
      ) : null}

      {parsed && parsed.parseErrors.length === 0 && rows.length > 0 ? (
        <div className="table-wrap" style={{ marginTop: "1rem" }}>
          <p className="muted">
            {preview == null
              ? `${rows.length} dòng chờ kiểm tra.`
              : preview.canCommit
                ? `${rows.length} dòng đạt — có thể ghi.`
                : `${rows.length} dòng, còn ${preview.issues.length} lỗi — chưa ghi.`}
          </p>
          <table className="data-table">
            <thead>
              <tr>
                <th>Dòng</th>
                <th>Mã</th>
                <th>Tên</th>
                <th>MST</th>
                <th>Vai trò</th>
                <th>Tài khoản</th>
                <th>Liên hệ</th>
                <th>Kết quả</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((row, index) => {
                const rowNo = index + 1;
                const messages = issuesByRow.get(rowNo) ?? [];
                const result =
                  messages.length > 0
                    ? messages.join(" ")
                    : preview == null
                      ? "Chờ kiểm tra"
                      : "Đạt";
                return (
                  <tr key={`${row.code}-${rowNo}`}>
                    <td>{rowNo}</td>
                    <td>{row.code || "—"}</td>
                    <td>{row.name || "—"}</td>
                    <td>{row.taxId || "—"}</td>
                    <td>{roleText(row)}</td>
                    <td>{row.bankAccountNumber ? `${row.bankName ?? ""} ${row.bankAccountNumber}`.trim() : "—"}</td>
                    <td>{row.contactName || "—"}</td>
                    <td>{result}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      ) : null}

      <div className="cta-row">
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
