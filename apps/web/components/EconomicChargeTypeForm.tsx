"use client";

import type { FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useState } from "react";

export function EconomicChargeTypeForm() {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    const fd = new FormData(e.currentTarget);
    const res = await fetch("/bff/economic-charge-types", {
      method: "POST",
      headers: { "Content-Type": "application/json", Accept: "application/json" },
      body: JSON.stringify({
        code: String(fd.get("code") ?? "").trim(),
        name: String(fd.get("name") ?? "").trim(),
      }),
    });
    setBusy(false);
    if (!res.ok) {
      const payload = (await res.json().catch(() => ({}))) as { message?: string };
      setError(payload.message || "Không lưu được khoản mục.");
      return;
    }
    e.currentTarget.reset();
    router.refresh();
  }

  return (
    <form onSubmit={onSubmit}>
      <div className="form-grid">
        <div className="field">
          <label htmlFor="code">Mã <span className="req">*</span></label>
          <input id="code" name="code" required maxLength={64} placeholder="PACKAGING" />
        </div>
        <div className="field">
          <label htmlFor="name">Tên <span className="req">*</span></label>
          <input id="name" name="name" required maxLength={256} placeholder="Phí đóng gói" />
        </div>
      </div>
      {error ? <div className="alert alert-error" role="alert">{error}</div> : null}
      <div className="cta-row">
        <button className="btn btn-primary" type="submit" disabled={busy}>{busy ? "Đang lưu…" : "Thêm khoản mục"}</button>
      </div>
    </form>
  );
}
