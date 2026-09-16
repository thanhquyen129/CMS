"use client";

import { useRouter } from "next/navigation";
import { useCallback, useState, useTransition } from "react";
import type { EscalateVariance } from "./EscalateVarianceButton";

type Action = "accept" | "clear" | "write-off";

type Props = {
  variance: EscalateVariance;
};

const LABELS: Record<Action, string> = {
  accept: "Chấp nhận",
  clear: "Xóa chênh lệch",
  "write-off": "Xóa nợ chênh lệch",
};

export function VarianceTransitionButtons({ variance }: Props) {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState<Action | null>(null);
  const [isPending, startTransition] = useTransition();

  const run = useCallback(
    async (action: Action) => {
      setBusy(action);
      setError(null);
      try {
        const res = await fetch(`/bff/variances/${variance.id}/${action}`, {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
            Accept: "application/json",
          },
          body: JSON.stringify({}),
        });
        if (res.status === 401) {
          window.location.href = "/login";
          return;
        }
        if (!res.ok) {
          const body = (await res.json().catch(() => ({}))) as {
            message?: string;
          };
          setError(body.message || "Không chuyển trạng thái được.");
          return;
        }
        startTransition(() => router.refresh());
      } catch {
        setError("Không kết nối được máy chủ. Thử lại sau.");
      } finally {
        setBusy(null);
      }
    },
    [router, variance.id]
  );

  if (variance.status.toLowerCase() !== "open") {
    return null;
  }

  return (
    <div className="toolbar-row" style={{ margin: "0.35rem 0 0", gap: "0.35rem" }}>
      {(Object.keys(LABELS) as Action[]).map((action) => (
        <button
          key={action}
          type="button"
          className="btn btn-ghost btn-sm"
          disabled={busy != null || isPending}
          onClick={() => run(action)}
        >
          {busy === action ? "Đang gửi…" : LABELS[action]}
        </button>
      ))}
      {error ? (
        <div className="alert alert-error" role="alert" style={{ marginTop: "0.35rem" }}>
          {error}
        </div>
      ) : null}
    </div>
  );
}
