"use client";

import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { formatApiErrorMessage } from "@/lib/api-error";

type Props = {
  rateCardId: string;
  code: string;
};

export function RetireRateCardButton({ rateCardId, code }: Props) {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [isPending, startTransition] = useTransition();

  async function retire() {
    if (!window.confirm(`Ngừng bảng giá ${code}? Phiên bản nháp đi cùng bảng giá.`)) {
      return;
    }
    setBusy(true);
    setError(null);
    try {
      const res = await fetch(`/bff/rate-cards/${rateCardId}`, {
        method: "DELETE",
        headers: { Accept: "application/json" },
      });
      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }
      if (!res.ok && res.status !== 204) {
        const body = (await res.json().catch(() => ({}))) as {
          message?: string;
          errors?: Record<string, string[]>;
        };
        setError(formatApiErrorMessage(body, "Không ngừng được bảng giá."));
        return;
      }
      startTransition(() => router.push("/rate-cards"));
    } catch {
      setError("Không kết nối được máy chủ.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="cta-row">
      <button type="button" className="btn btn-ghost" onClick={() => void retire()} disabled={busy || isPending}>
        {busy ? "Đang ngừng…" : "Ngừng bảng giá"}
      </button>
      {error ? (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      ) : null}
    </div>
  );
}
