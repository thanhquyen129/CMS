"use client";

import type { FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";

export function UpsertFxRateForm() {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [isPending, startTransition] = useTransition();

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setError(null);
    setSubmitting(true);
    const fd = new FormData(e.currentTarget);
    const rate = Number(String(fd.get("rate") ?? "").replace(",", "."));
    if (!Number.isFinite(rate) || rate <= 0) {
      setError("Tỷ giá phải lớn hơn 0.");
      setSubmitting(false);
      return;
    }
    const body = {
      fromCurrencyCode: String(fd.get("from") ?? "")
        .trim()
        .toUpperCase(),
      toCurrencyCode: String(fd.get("to") ?? "")
        .trim()
        .toUpperCase(),
      rateDate: String(fd.get("rateDate") ?? "").trim(),
      rate,
      source: "manual",
      note: String(fd.get("note") ?? "").trim() || null,
    };
    if (!body.fromCurrencyCode || !body.toCurrencyCode || !body.rateDate) {
      setError("Nhập cặp tiền tệ và ngày tỷ giá.");
      setSubmitting(false);
      return;
    }
    try {
      const res = await fetch("/bff/admin/fx-rates", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Accept: "application/json",
        },
        body: JSON.stringify(body),
      });
      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }
      if (!res.ok) {
        const payload = (await res.json().catch(() => ({}))) as {
          message?: string;
        };
        setError(payload.message || "Không lưu được tỷ giá.");
        return;
      }
      (e.target as HTMLFormElement).reset();
      startTransition(() => router.refresh());
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      setSubmitting(false);
    }
  }

  const busy = submitting || isPending;
  const today = new Date().toISOString().slice(0, 10);

  return (
    <form className="receive-form" onSubmit={onSubmit} noValidate>
      {error ? (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      ) : null}
      <div className="form-grid">
        <div className="field">
          <label htmlFor="fx-from">Từ</label>
          <input
            id="fx-from"
            name="from"
            required
            maxLength={3}
            defaultValue="USD"
            disabled={busy}
          />
        </div>
        <div className="field">
          <label htmlFor="fx-to">Sang</label>
          <input
            id="fx-to"
            name="to"
            required
            maxLength={3}
            defaultValue="VND"
            disabled={busy}
          />
        </div>
        <div className="field">
          <label htmlFor="fx-date">Ngày tỷ giá</label>
          <input
            id="fx-date"
            name="rateDate"
            type="date"
            required
            defaultValue={today}
            disabled={busy}
          />
        </div>
        <div className="field">
          <label htmlFor="fx-rate">Tỷ giá</label>
          <input
            id="fx-rate"
            name="rate"
            inputMode="decimal"
            required
            disabled={busy}
          />
        </div>
        <div className="field">
          <label htmlFor="fx-note">Ghi chú</label>
          <input id="fx-note" name="note" maxLength={512} disabled={busy} />
        </div>
      </div>
      <div className="cta-row">
        <button className="btn" type="submit" disabled={busy}>
          {busy ? "Đang lưu…" : "Ghi tỷ giá"}
        </button>
      </div>
    </form>
  );
}
