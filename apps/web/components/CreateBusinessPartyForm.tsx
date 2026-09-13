"use client";

import type { FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";

export function CreateBusinessPartyForm() {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [isPending, startTransition] = useTransition();

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setError(null);
    setSubmitting(true);

    const fd = new FormData(e.currentTarget);
    const code = String(fd.get("code") ?? "").trim();
    const name = String(fd.get("name") ?? "").trim();

    if (!code || !name) {
      setError("Nhập mã và tên đối tác.");
      setSubmitting(false);
      return;
    }

    try {
      const res = await fetch("/bff/admin/parties", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Accept: "application/json",
        },
        body: JSON.stringify({ code, name }),
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
            (res.status === 409
              ? "Mã đối tác đã tồn tại."
              : "Tạo đối tác thất bại.")
        );
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
          <label htmlFor="partyCode">Mã đối tác</label>
          <input
            id="partyCode"
            name="code"
            type="text"
            required
            maxLength={64}
            disabled={busy}
          />
        </div>
        <div className="field">
          <label htmlFor="partyName">Tên đối tác</label>
          <input
            id="partyName"
            name="name"
            type="text"
            required
            maxLength={256}
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
          {busy ? "Đang lưu…" : "Thêm đối tác"}
        </button>
      </div>
    </form>
  );
}
