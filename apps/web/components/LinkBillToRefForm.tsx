"use client";

import type { FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";

type Props = {
  endpoint: string;
  billLabel?: string;
};

/** Resolve Bill by number via search, then POST canonical link. */
export function LinkBillToRefForm({ endpoint, billLabel = "Bill" }: Props) {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [info, setInfo] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [isPending, startTransition] = useTransition();

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setError(null);
    setInfo(null);
    setBusy(true);
    const fd = new FormData(e.currentTarget);
    const billNo = String(fd.get("billNo") ?? "").trim();
    if (!billNo) {
      setError(`Nhập số ${billLabel}.`);
      setBusy(false);
      return;
    }

    try {
      const searchRes = await fetch(
        `/bff/search?q=${encodeURIComponent(billNo)}`,
        { headers: { Accept: "application/json" } }
      );
      if (searchRes.status === 401) {
        window.location.href = "/login";
        return;
      }
      if (!searchRes.ok) {
        setError("Không tìm được Bill.");
        return;
      }
      const hits = (await searchRes.json()) as {
        entityType?: string;
        id?: string;
        code?: string;
      }[];
      const bill = hits.find(
        (h) =>
          h.entityType === "bill" &&
          h.code?.toLowerCase() === billNo.toLowerCase()
      ) ?? hits.find((h) => h.entityType === "bill");
      if (!bill?.id) {
        setError(`Không thấy ${billLabel} «${billNo}».`);
        return;
      }

      const res = await fetch(
        `${endpoint}/${encodeURIComponent(bill.id)}`,
        {
          method: "POST",
          headers: { Accept: "application/json" },
        }
      );
      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }
      if (!res.ok) {
        const payload = (await res.json().catch(() => ({}))) as {
          message?: string;
        };
        setError(payload.message || "Không gắn được Bill.");
        return;
      }
      setInfo(`Đã gắn ${billLabel} ${bill.code ?? billNo}.`);
      (e.target as HTMLFormElement).reset();
      startTransition(() => router.refresh());
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      setBusy(false);
    }
  }

  const loading = busy || isPending;

  return (
    <form className="receive-form" onSubmit={onSubmit} noValidate>
      <h3 className="section-title sm">Gắn {billLabel}</h3>
      {error ? (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      ) : null}
      {info ? (
        <div className="alert alert-info" role="status">
          {info}
        </div>
      ) : null}
      <div className="field">
        <label htmlFor="link-bill-no">Số {billLabel}</label>
        <input
          id="link-bill-no"
          name="billNo"
          required
          maxLength={64}
          disabled={loading}
        />
      </div>
      <div className="cta-row">
        <button className="btn" type="submit" disabled={loading}>
          {loading ? "Đang gắn…" : `Gắn ${billLabel}`}
        </button>
      </div>
    </form>
  );
}
