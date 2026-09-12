"use client";

import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { term, type TerminologyMap } from "@/lib/terminology";

type Props = {
  terms: TerminologyMap;
  lineId: string;
};

export function IgnoreBankFeedLineButton({ terms, lineId }: Props) {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [isPending, startTransition] = useTransition();

  const ignoreLabel = term(terms, "BANK_FEED_IGNORED", "Đã bỏ qua");

  async function onIgnore() {
    const reason = window.prompt("Lý do bỏ qua (tuỳ chọn):") ?? "";
    setError(null);
    setSubmitting(true);
    try {
      const res = await fetch(`/bff/bank-feed/lines/${lineId}/ignore`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Accept: "application/json",
        },
        body: JSON.stringify({ ignoreReason: reason.trim() || null }),
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
              ? "Không bỏ qua được (đã khớp). Tải lại trang."
              : "Bỏ qua dòng thất bại.")
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
    <span className="action-cluster">
      <button
        type="button"
        className="btn btn-ghost btn-sm"
        disabled={busy}
        onClick={() => void onIgnore()}
      >
        {busy ? "…" : ignoreLabel.replace(/^Đã /, "Bỏ qua")}
      </button>
      {error ? (
        <span className="muted small" role="alert">
          {error}
        </span>
      ) : null}
    </span>
  );
}
