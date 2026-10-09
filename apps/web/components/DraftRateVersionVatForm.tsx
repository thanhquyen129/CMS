"use client";

import type { FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";

type Props = {
  versionId: string;
  vatRate: number | null;
};

export function DraftRateVersionVatForm({ versionId, vatRate }: Props) {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [isPending, startTransition] = useTransition();
  const busy = submitting || isPending;

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setError(null);
    setSubmitting(true);
    const raw = String(new FormData(e.currentTarget).get("vatRate") ?? "").trim().replace(",", ".");
    const body = { vatRate: raw ? Number(raw) : null };
    if (raw && !Number.isFinite(body.vatRate)) {
      setError("Thuế suất VAT không hợp lệ.");
      setSubmitting(false);
      return;
    }

    try {
      const res = await fetch(`/bff/rate-versions/${versionId}/vat`, {
        method: "PUT",
        headers: { "Content-Type": "application/json", Accept: "application/json" },
        body: JSON.stringify(body),
      });
      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }
      if (!res.ok) {
        const payload = (await res.json().catch(() => null)) as { detail?: string; title?: string } | null;
        setError(payload?.detail || payload?.title || "Không lưu được thuế suất VAT.");
        setSubmitting(false);
        return;
      }
      startTransition(() => router.refresh());
    } catch {
      setError("Không lưu được thuế suất VAT.");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <form className="inline-form" onSubmit={onSubmit}>
      <div className="field">
        <label htmlFor={`vat-${versionId}`}>Thuế suất VAT (%)</label>
        <input
          id={`vat-${versionId}`}
          name="vatRate"
          inputMode="decimal"
          defaultValue={vatRate ?? ""}
          placeholder="Để trống = chưa khai báo"
          disabled={busy}
        />
        <p className="muted small">
          {vatRate == null ? "Chưa khai báo. Không mặc định 0%." : `Đang khai báo ${vatRate}%.`}
        </p>
      </div>
      {error ? (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      ) : null}
      <button className="btn" type="submit" disabled={busy}>
        Lưu VAT
      </button>
    </form>
  );
}
