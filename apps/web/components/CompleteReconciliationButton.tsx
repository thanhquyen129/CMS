"use client";

import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { term, type TerminologyMap } from "@/lib/terminology";

type Props = {
  terms: TerminologyMap;
  reconciliationId: string;
};

export function CompleteReconciliationButton({
  terms,
  reconciliationId,
}: Props) {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [isPending, startTransition] = useTransition();

  const reconLabel = term(terms, "RECONCILIATION", "Đối soát");

  async function onComplete() {
    setError(null);
    setSubmitting(true);
    try {
      const res = await fetch(
        `/bff/reconciliations/${reconciliationId}/complete`,
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
        setError(
          payload.message ||
            (res.status === 409
              ? "Không hoàn tất được (trạng thái). Tải lại trang."
              : `Hoàn tất ${reconLabel.toLowerCase()} thất bại.`)
        );
        return;
      }

      startTransition(() => router.refresh());
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      setSubmitting(false);
    }
  }

  const busy = submitting || isPending;

  return (
    <div className="action-cluster">
      <button
        type="button"
        className="btn"
        disabled={busy}
        onClick={() => void onComplete()}
      >
        {busy ? "Đang hoàn tất…" : "Hoàn tất phiên"}
      </button>
      {error ? (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      ) : null}
    </div>
  );
}
