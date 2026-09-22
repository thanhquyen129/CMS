"use client";

import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { newIdempotencyKey, withIdempotency } from "@/lib/idempotency";

type Kind = "order" | "shipment" | "leg" | "movement";

type Props = {
  billId: string;
  relatedId: string;
  kind: Kind;
  label: string;
};

function pathFor(kind: Kind, billId: string, relatedId: string): string {
  switch (kind) {
    case "order":
      return `/bff/orders/${relatedId}/bills/${billId}`;
    case "shipment":
      return `/bff/bills/${billId}/shipments/${relatedId}`;
    case "leg":
      return `/bff/bills/${billId}/legs/${relatedId}`;
    case "movement":
      return `/bff/bills/${billId}/movements/${relatedId}`;
  }
}

export function UnlinkRelationButton({ billId, relatedId, kind, label }: Props) {
  const router = useRouter();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [isPending, startTransition] = useTransition();

  async function onUnlink() {
    const reason = window.prompt(
      `Gỡ liên kết ${label}? Có thể ghi lý do (tuỳ chọn):`,
      ""
    );
    if (reason === null) return;

    setBusy(true);
    setError(null);
    try {
      const qs = reason.trim()
        ? `?reason=${encodeURIComponent(reason.trim())}`
        : "";
      const res = await fetch(`${pathFor(kind, billId, relatedId)}${qs}`, {
        method: "DELETE",
        headers: withIdempotency({}, newIdempotencyKey("unlink")),
      });
      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }
      if (!res.ok && res.status !== 204) {
        const body = (await res.json().catch(() => ({}))) as { message?: string };
        setError(body.message || "Gỡ liên kết thất bại.");
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
    <span className="inline-actions">
      <button
        type="button"
        className="btn btn-ghost btn-sm"
        onClick={() => void onUnlink()}
        disabled={busy || isPending}
      >
        Gỡ liên kết
      </button>
      {error ? (
        <span className="alert alert-error" role="alert">
          {error}
        </span>
      ) : null}
    </span>
  );
}
