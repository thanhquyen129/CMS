"use client";

import type { FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";

type Props = {
  componentId: string;
  name: string;
  amount: number;
  currencyCode: string;
  financialNature: string;
  costTypeCode?: string | null;
  revenueTypeCode?: string | null;
};

export function PricingRuleComponentActions({
  componentId,
  name,
  amount,
  currencyCode,
  financialNature,
  costTypeCode,
  revenueTypeCode,
}: Props) {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [isPending, startTransition] = useTransition();

  async function onSave(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setError(null);
    const fd = new FormData(e.currentTarget);
    const nextAmount = Number(String(fd.get("amount") ?? "").replace(",", "."));
    const nextName = String(fd.get("name") ?? "").trim();
    if (!nextName || !Number.isFinite(nextAmount) || nextAmount < 0) {
      setError("Tên và số tiền thành phần không hợp lệ.");
      return;
    }
    setBusy(true);
    try {
      const res = await fetch(`/bff/pricing-rules/components/${componentId}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json", Accept: "application/json" },
        body: JSON.stringify({
          name: nextName,
          financialNature,
          costTypeCode: costTypeCode ?? null,
          revenueTypeCode: revenueTypeCode ?? null,
          amount: nextAmount,
          currencyCode,
        }),
      });
      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }
      if (!res.ok && res.status !== 204) {
        const payload = (await res.json().catch(() => ({}))) as { message?: string };
        setError(payload.message || "Không sửa được thành phần.");
        return;
      }
      startTransition(() => router.refresh());
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      setBusy(false);
    }
  }

  async function onDelete() {
    if (!window.confirm("Xóa thành phần này trên phiên bản nháp?")) return;
    setError(null);
    setBusy(true);
    try {
      const res = await fetch(`/bff/pricing-rules/components/${componentId}`, {
        method: "DELETE",
        headers: { Accept: "application/json" },
      });
      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }
      if (!res.ok && res.status !== 204) {
        const payload = (await res.json().catch(() => ({}))) as { message?: string };
        setError(payload.message || "Không xóa được thành phần.");
        return;
      }
      startTransition(() => router.refresh());
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      setBusy(false);
    }
  }

  const waiting = busy || isPending;

  return (
    <form onSubmit={onSave} style={{ marginTop: "0.35rem" }}>
      {error ? (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      ) : null}
      <div className="form-grid">
        <input name="name" defaultValue={name} aria-label="Tên thành phần" disabled={waiting} />
        <input
          name="amount"
          defaultValue={String(amount)}
          inputMode="decimal"
          aria-label="Số tiền thành phần"
          disabled={waiting}
        />
        <button type="submit" className="btn btn-sm" disabled={waiting}>
          Lưu
        </button>
        <button type="button" className="btn btn-sm btn-ghost" onClick={onDelete} disabled={waiting}>
          Xóa
        </button>
      </div>
    </form>
  );
}
