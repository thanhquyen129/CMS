"use client";

import type { FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";

type Props = {
  rateCardId: string;
};

export function CreateRateVersionForm({ rateCardId }: Props) {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [isPending, startTransition] = useTransition();

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setError(null);
    setSubmitting(true);

    const fd = new FormData(e.currentTarget);
    const fromRaw = String(fd.get("effectiveFrom") ?? "").trim();
    const toRaw = String(fd.get("effectiveTo") ?? "").trim();
    const body = {
      effectiveFrom: fromRaw ? new Date(fromRaw).toISOString() : null,
      effectiveTo: toRaw ? new Date(toRaw).toISOString() : null,
      note: String(fd.get("note") ?? "").trim() || null,
    };

    try {
      const res = await fetch(`/bff/rate-cards/${rateCardId}/versions`, {
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
        setError(
          payload.message ||
            (res.status === 403
              ? "Bạn không có quyền tạo phiên bản."
              : "Tạo phiên bản thất bại.")
        );
        return;
      }

      startTransition(() => router.refresh());
      e.currentTarget.reset();
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      setSubmitting(false);
    }
  }

  const busy = submitting || isPending;

  return (
    <form className="receive-form" onSubmit={onSubmit} noValidate>
      <h3 className="section-title sm">Thêm phiên bản nháp</h3>
      {error ? (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      ) : null}
      <div className="form-grid">
        <div className="field">
          <label htmlFor="effectiveFrom">Hiệu lực từ (tuỳ chọn)</label>
          <input id="effectiveFrom" name="effectiveFrom" type="date" />
        </div>
        <div className="field">
          <label htmlFor="effectiveTo">Hiệu lực đến (tuỳ chọn)</label>
          <input id="effectiveTo" name="effectiveTo" type="date" />
        </div>
        <div className="field field-span">
          <label htmlFor="note">Ghi chú</label>
          <input id="note" name="note" maxLength={512} />
        </div>
      </div>
      <div className="cta-row">
        <button className="btn" type="submit" disabled={busy}>
          {busy ? "Đang tạo…" : "Tạo phiên bản nháp"}
        </button>
      </div>
    </form>
  );
}
