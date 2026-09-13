"use client";

import type { ChangeEvent, FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { term, type TerminologyMap } from "@/lib/terminology";

const CSV_HEADER =
  "valueDate,amount,currencyCode,direction,bankReference,counterpartyName,description";

type ImportResult = {
  imported?: number;
  skipped?: number;
  errors?: string[];
};

type Props = {
  terms: TerminologyMap;
};

export function ImportBankFeedCsvForm({ terms }: Props) {
  const router = useRouter();
  const [csv, setCsv] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<ImportResult | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [isPending, startTransition] = useTransition();

  const lineLabel = term(terms, "BANK_FEED_LINE", "Dòng sao kê");

  function onFileChange(e: ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    if (!file) return;
    const reader = new FileReader();
    reader.onload = () => {
      setCsv(String(reader.result ?? ""));
      setResult(null);
      setError(null);
    };
    reader.readAsText(file);
    e.target.value = "";
  }

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setError(null);
    setResult(null);

    const text = csv.trim();
    if (!text) {
      setError("Dán hoặc chọn file CSV trước khi nhập.");
      return;
    }

    setSubmitting(true);

    try {
      const res = await fetch("/bff/bank-feed/lines/import", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Accept: "application/json",
        },
        body: JSON.stringify({ csv: text }),
      });

      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }

      const payload = (await res.json().catch(() => ({}))) as ImportResult & {
        message?: string;
      };

      if (!res.ok) {
        setError(
          payload.message ||
            (res.status === 403
              ? `Bạn không có quyền nhập ${lineLabel.toLowerCase()}.`
              : "Nhập CSV thất bại.")
        );
        return;
      }

      setResult(payload);
      startTransition(() => router.refresh());
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      setSubmitting(false);
    }
  }

  const busy = submitting || isPending;

  return (
    <form className="receive-form" onSubmit={onSubmit}>
      <p className="note">
        Nhập hàng loạt dòng sao kê từ CSV. Header chuẩn:{" "}
        <code className="mono-id">{CSV_HEADER}</code>. Chiều:{" "}
        <code>credit</code> hoặc <code>debit</code>.
      </p>

      <div className="form-grid">
        <div className="field field-span">
          <label htmlFor="csvFile">Chọn file CSV</label>
          <input
            id="csvFile"
            type="file"
            accept=".csv,text/csv"
            onChange={onFileChange}
            disabled={busy}
          />
        </div>
        <div className="field field-span">
          <label htmlFor="csvText">Nội dung CSV</label>
          <textarea
            id="csvText"
            rows={8}
            value={csv}
            onChange={(e) => {
              setCsv(e.target.value);
              setResult(null);
            }}
            placeholder={`${CSV_HEADER}\n2026-09-13,1500000,VND,credit,REF-001,ABC Logistics,Thu cước tháng 8`}
            disabled={busy}
          />
        </div>
      </div>

      {error ? (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      ) : null}

      {result ? (
        <div className="alert" role="status">
          <strong>Kết quả nhập:</strong>{" "}
          {typeof result.imported === "number"
            ? `${result.imported} dòng đã nhập`
            : "Đã xử lý"}
          {typeof result.skipped === "number"
            ? ` · ${result.skipped} bỏ qua`
            : ""}
          {result.errors && result.errors.length > 0 ? (
            <ul className="small" style={{ marginTop: "0.5rem" }}>
              {result.errors.map((msg, i) => (
                <li key={i}>{msg}</li>
              ))}
            </ul>
          ) : null}
        </div>
      ) : null}

      <div className="cta-row">
        <button type="submit" className="btn" disabled={busy}>
          {busy ? "Đang nhập…" : "Nhập CSV"}
        </button>
      </div>
    </form>
  );
}
