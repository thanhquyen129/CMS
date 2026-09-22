"use client";

import type { ChangeEvent, FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { newIdempotencyKey, withIdempotency } from "@/lib/idempotency";

type Issue = { row: number; field: string; message: string };
type Preview = { canCommit: boolean; issues: Issue[] };

type RateImportCard = {
  code: string;
  name: string;
  partyType: string;
  currencyCode: string;
  transportMode?: string | null;
  routeCode?: string | null;
  carrierName?: string | null;
  note?: string | null;
  rules?: Array<{
    code: string;
    name: string;
    calcMethod: string;
    unitAmount: number;
    currencyCode?: string | null;
    chargeCode?: string | null;
  }>;
};

/** Minimal JSON batch — one card with one fixed rule is enough to start. */
const SAMPLE = `{
  "cards": [
    {
      "code": "RC-IMP-DEMO",
      "name": "Bảng giá nhập demo",
      "partyType": "vendor",
      "currencyCode": "USD",
      "transportMode": "air",
      "rules": [
        {
          "code": "FREIGHT",
          "name": "Cước chính",
          "calcMethod": "fixed",
          "unitAmount": 100
        }
      ]
    }
  ]
}`;

export function ImportRateCardsForm() {
  const router = useRouter();
  const [jsonText, setJsonText] = useState(SAMPLE);
  const [error, setError] = useState<string | null>(null);
  const [preview, setPreview] = useState<Preview | null>(null);
  const [count, setCount] = useState<number | null>(null);
  const [busy, setBusy] = useState(false);
  const [isPending, startTransition] = useTransition();

  function onFileChange(e: ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    if (!file) return;
    const reader = new FileReader();
    reader.onload = () => {
      setJsonText(String(reader.result ?? ""));
      setPreview(null);
      setCount(null);
      setError(null);
    };
    reader.readAsText(file);
    e.target.value = "";
  }

  function parseCards(): RateImportCard[] | null {
    try {
      const raw = JSON.parse(jsonText) as { cards?: RateImportCard[] } | RateImportCard[];
      const cards = Array.isArray(raw) ? raw : raw.cards;
      if (!cards || !Array.isArray(cards) || cards.length === 0) {
        setError("JSON phải có mảng cards (ít nhất 1 bảng giá).");
        return null;
      }
      return cards;
    } catch {
      setError("JSON không hợp lệ.");
      return null;
    }
  }

  async function run(mode: "preview" | "commit") {
    setError(null);
    setCount(null);
    const cards = parseCards();
    if (!cards) return;

    setBusy(true);
    try {
      const path =
        mode === "preview"
          ? "/bff/rate-imports/preview"
          : "/bff/rate-imports/commit";
      const headers =
        mode === "commit"
          ? withIdempotency(
              { "Content-Type": "application/json" },
              newIdempotencyKey("rate-import")
            )
          : { "Content-Type": "application/json", Accept: "application/json" };

      const res = await fetch(path, {
        method: "POST",
        headers,
        body: JSON.stringify({ cards }),
      });

      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }

      const payload = (await res.json().catch(() => ({}))) as Preview & {
        count?: number;
        message?: string;
      };

      if (!res.ok) {
        setError(
          payload.message ||
            (res.status === 409
              ? "Không ghi được — còn lỗi. Xem bảng kiểm tra."
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
        setCount(payload.count ?? cards.length);
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
        Nhập hàng loạt bảng giá (JSON). Phiên bản mới ở trạng thái <strong>nháp</strong> —
        không ghi đè phiên bản đã Published. Một lỗi trên bất kỳ thẻ nào thì không ghi gì.
        <code>partyType</code>: vendor | customer · <code>calcMethod</code>: fixed,
        unit_rate, weight_break_pivot, …
      </p>

      <div className="form-grid">
        <div className="field field-span">
          <label htmlFor="rate-file">Chọn file JSON</label>
          <input
            id="rate-file"
            type="file"
            accept=".json,application/json"
            onChange={onFileChange}
            disabled={loading}
          />
        </div>
        <div className="field field-span">
          <label htmlFor="rate-json">Nội dung JSON</label>
          <textarea
            id="rate-json"
            rows={16}
            value={jsonText}
            onChange={(e) => {
              setJsonText(e.target.value);
              setPreview(null);
              setCount(null);
            }}
            disabled={loading}
            className="mono-id"
          />
        </div>
      </div>

      {error ? (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      ) : null}

      {count != null ? (
        <div className="alert alert-success" role="status">
          Đã tạo {count} bảng giá (nháp). Mở Danh sách bảng giá để phát hành.
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
        <button type="submit" className="btn" disabled={loading || !jsonText.trim()}>
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
