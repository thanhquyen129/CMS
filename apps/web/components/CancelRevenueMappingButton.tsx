"use client";

import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { formatApiErrorMessage } from "@/lib/api-error";
import { withRowVersion } from "@/lib/idempotency";

type Props = {
  mappingId: string;
  rowVersion?: string | null;
};

export function CancelRevenueMappingButton({ mappingId, rowVersion }: Props) {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [isPending, startTransition] = useTransition();

  async function cancel() {
    setBusy(true);
    setError(null);
    try {
      const res = await fetch(`/bff/revenue-mappings/${mappingId}/cancel`, {
        method: "POST",
        headers: withRowVersion({ Accept: "application/json" }, rowVersion),
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
        setError(formatApiErrorMessage(body, "Không hủy được phiên chia."));
        return;
      }
      startTransition(() => router.refresh());
    } catch {
      setError("Không kết nối được máy chủ.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="cta-row">
      <button type="button" className="btn btn-ghost btn-sm" disabled={busy || isPending} onClick={() => void cancel()}>
        {busy ? "Đang hủy…" : "Hủy phiên chia"}
      </button>
      {error ? (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      ) : null}
    </div>
  );
}
