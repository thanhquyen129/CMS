"use client";

import { useState, useTransition } from "react";
import { useRouter } from "next/navigation";

type Props = {
  rateCardId: string;
  sourceVersionId?: string;
  sourceVersionNo?: number;
  label?: string;
  disabled?: boolean;
  variant?: "default" | "small" | "primary";
};

export function CreateRateVersionButton({
  rateCardId,
  sourceVersionId,
  sourceVersionNo,
  label,
  disabled,
  variant = "default",
}: Props) {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [isPending, startTransition] = useTransition();

  async function onClick() {
    if (submitting || isPending) return;
    setError(null);
    setSubmitting(true);

    try {
      const res = await fetch(`/bff/rate-cards/${rateCardId}/versions`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Accept: "application/json",
        },
        body: JSON.stringify({
          cloneFromVersionId: sourceVersionId || null,
        }),
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
            (res.status === 403
              ? "Bạn không có quyền tạo phiên bản bảng giá này."
              : "Tạo phiên bản mới thất bại.")
        );
        return;
      }

      startTransition(() => {
        router.push(`/rate-cards/${rateCardId}/edit`);
        router.refresh();
      });
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      setSubmitting(false);
    }
  }

  const busy = submitting || isPending;
  const buttonClass =
    variant === "small"
      ? "btn btn-sm btn-outline"
      : variant === "primary"
        ? "btn btn-primary"
        : "btn";

  return (
    <div style={{ display: "inline-block" }}>
      {error ? (
        <div className="alert alert-error" role="alert" style={{ marginBottom: "0.5rem" }}>
          {error}
        </div>
      ) : null}
      <button
        type="button"
        className={buttonClass}
        disabled={disabled || busy}
        onClick={onClick}
      >
        {busy ? "Đang tạo phiên bản…" : label ?? "Tạo phiên bản mới"}
      </button>
    </div>
  );
}

