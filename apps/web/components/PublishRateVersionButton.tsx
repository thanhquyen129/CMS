"use client";

import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";

type Props = {
  versionId: string;
  versionNo: number;
};

export function PublishRateVersionButton({ versionId, versionNo }: Props) {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [isPending, startTransition] = useTransition();

  async function onPublish() {
    if (
      !window.confirm(
        `Phát hành phiên bản v${versionNo}? Sau khi phát hành không sửa quy tắc trên phiên bản này.`
      )
    ) {
      return;
    }

    setError(null);
    setBusy(true);
    try {
      const res = await fetch(`/bff/rate-versions/${versionId}/publish`, {
        method: "POST",
        headers: { Accept: "application/json" },
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
              ? "Không phát hành được (thiếu quy tắc hoặc đã phát hành)."
              : "Phát hành thất bại.")
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

  const loading = busy || isPending;

  return (
    <div>
      {error ? (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      ) : null}
      <button
        type="button"
        className="btn"
        disabled={loading}
        onClick={onPublish}
      >
        {loading ? "Đang phát hành…" : `Phát hành v${versionNo}`}
      </button>
    </div>
  );
}
