"use client";

import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { MissingFieldList } from "@/components/RatingReadinessBox";
import type { ApiError, RatingMissingField } from "@/lib/opref-edit";

/** Rerate on the same rate version; the old rating is kept as "Đã thay thế" (ADR-0039 D05). */
export function RerateRatingButton({
  billId,
  ratingId,
  rateVersionId,
  editAnchor = "bill-edit",
}: {
  billId: string;
  ratingId: string;
  rateVersionId: string;
  editAnchor?: string;
}) {
  const router = useRouter();
  const [busy, setBusy] = useState(false);
  const [isPending, startTransition] = useTransition();
  const [error, setError] = useState<string | null>(null);
  const [missing, setMissing] = useState<RatingMissingField[]>([]);

  async function onClick() {
    setError(null);
    setMissing([]);
    setBusy(true);
    try {
      const res = await fetch("/bff/ratings", {
        method: "POST",
        headers: { "Content-Type": "application/json", Accept: "application/json" },
        body: JSON.stringify({ billId, rateVersionId, supersedesRatingId: ratingId }),
      });
      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }
      if (!res.ok) {
        const payload = (await res.json().catch(() => ({}))) as ApiError;
        setMissing(payload.missing ?? []);
        setError(
          payload.code === "rating_not_ready"
            ? "Chưa đủ dữ liệu để tính giá lại."
            : payload.message ?? (res.status === 403 ? "Bạn không có quyền tính lại giá." : "Tính giá lại thất bại.")
        );
        return;
      }
      startTransition(() => router.refresh());
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="stack">
      <button type="button" className="btn btn-sm" disabled={busy || isPending} onClick={onClick}>
        {busy || isPending ? "Đang tính…" : "Tính giá lại"}
      </button>
      {error ? (
        <div className="alert alert-error" role="alert">
          {error}
          {missing.length > 0 ? <MissingFieldList missing={missing} editAnchor={editAnchor} /> : null}
        </div>
      ) : null}
    </div>
  );
}
