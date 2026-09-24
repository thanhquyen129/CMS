"use client";

import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { newIdempotencyKey, withIdempotency, withRowVersion } from "@/lib/idempotency";

export function AllocationSessionActions({
  allocationId,
  status,
  rowVersion,
}: {
  allocationId: string;
  status: string;
  rowVersion?: string | null;
}) {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [pending, startTransition] = useTransition();
  const current = status.toLowerCase();

  async function run(action: "calculate" | "submit" | "cancel") {
    setError(null);
    setBusy(true);
    try {
      const res = await fetch(`/bff/cost-allocations/${allocationId}/${action}`, {
        method: "POST",
        headers: withRowVersion(withIdempotency({}, newIdempotencyKey(`alloc-${action}`)), rowVersion),
      });
      if (!res.ok) {
        const body = (await res.json().catch(() => ({}))) as { message?: string };
        setError(body.message || "Không cập nhật được phiên phân bổ.");
        return;
      }
      startTransition(() => router.refresh());
    } catch {
      setError("Không kết nối được máy chủ.");
    } finally {
      setBusy(false);
    }
  }

  const disabled = busy || pending;
  return (
    <span className="cta-row">
      {current === "draft" ? (
        <button className="btn btn-sm" type="button" disabled={disabled} onClick={() => run("calculate")}>
          Tính phân bổ
        </button>
      ) : null}
      {current === "calculated" ? (
        <button className="btn btn-sm btn-ghost" type="button" disabled={disabled} onClick={() => run("submit")}>
          Gửi duyệt
        </button>
      ) : null}
      {current === "draft" || current === "calculated" || current === "pending_approval" ? (
        <button className="btn btn-sm btn-ghost" type="button" disabled={disabled} onClick={() => run("cancel")}>
          Hủy phiên
        </button>
      ) : null}
      {error ? <span className="alert alert-error">{error}</span> : null}
    </span>
  );
}
