"use client";

import type { FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";

export function CreateCurrencyForm() {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [isPending, startTransition] = useTransition();

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setError(null);
    setSubmitting(true);

    const fd = new FormData(e.currentTarget);
    const code = String(fd.get("code") ?? "")
      .trim()
      .toUpperCase();
    const name = String(fd.get("name") ?? "").trim();

    if (!code || !name) {
      setError("Nhập mã và tên tiền tệ.");
      setSubmitting(false);
      return;
    }

    const decimalPlaces = code === "VND" ? 0 : 2;

    try {
      const res = await fetch("/bff/admin/currencies", {
        method: "PUT",
        headers: {
          "Content-Type": "application/json",
          Accept: "application/json",
        },
        body: JSON.stringify({
          code,
          name,
          decimalPlaces,
          isActive: true,
        }),
      });

      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }

      if (!res.ok) {
        const payload = (await res.json().catch(() => ({}))) as {
          message?: string;
        };
        setError(payload.message || "Tạo/cập nhật tiền tệ thất bại.");
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

  return (
    <form className="receive-form" onSubmit={onSubmit}>
      <div className="form-grid">
        <div className="field">
          <label htmlFor="currencyCode">Mã tiền tệ</label>
          <input
            id="currencyCode"
            name="code"
            type="text"
            required
            maxLength={3}
            defaultValue="VND"
            disabled={busy}
          />
        </div>
        <div className="field">
          <label htmlFor="currencyName">Tên tiền tệ</label>
          <input
            id="currencyName"
            name="name"
            type="text"
            required
            maxLength={128}
            disabled={busy}
          />
        </div>
      </div>

      {error ? (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      ) : null}

      <div className="cta-row">
        <button type="submit" className="btn" disabled={busy}>
          {busy ? "Đang lưu…" : "Thêm tiền tệ"}
        </button>
      </div>
    </form>
  );
}
