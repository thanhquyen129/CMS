"use client";

import { useRouter } from "next/navigation";
import { useCallback, useId, useState, useTransition } from "react";

type Action = "mark-retried" | "dead-letter";

type Props = {
  errorId: string;
  recoveryStatus: string;
};

export function IntegrationErrorActions({ errorId, recoveryStatus }: Props) {
  const router = useRouter();
  const dialogTitleId = useId();
  const [open, setOpen] = useState<Action | null>(null);
  const [note, setNote] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [isPending, startTransition] = useTransition();

  const status = recoveryStatus?.toLowerCase() ?? "";
  const canAct = status === "pending";

  const close = useCallback(() => {
    if (submitting) return;
    setOpen(null);
  }, [submitting]);

  const runAction = useCallback(async () => {
    if (!open) return;

    setSubmitting(true);
    setError(null);

    const endpoint =
      open === "mark-retried"
        ? `/bff/integration-errors/${errorId}/mark-retried`
        : `/bff/integration-errors/${errorId}/dead-letter`;

    const body = { note: note.trim() || null };

    try {
      const res = await fetch(endpoint, {
        method: "POST",
        headers: {
          Accept: "application/json",
          "Content-Type": "application/json",
        },
        body: JSON.stringify(body),
      });

      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }

      if (!res.ok && res.status !== 204) {
        const payload = (await res.json().catch(() => ({}))) as {
          message?: string;
        };
        setError(
          payload.message ||
            (res.status === 409
              ? "Không thao tác được (trạng thái đã đổi). Tải lại trang."
              : "Thao tác phục hồi tích hợp thất bại.")
        );
        return;
      }

      setOpen(null);
      setNote("");
      startTransition(() => router.refresh());
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      setSubmitting(false);
    }
  }, [errorId, note, open, router]);

  if (!canAct) {
    return <span className="muted">—</span>;
  }

  return (
    <>
      <div className="row-actions">
        <button
          type="button"
          className="btn btn-sm"
          onClick={() => {
            setError(null);
            setNote("");
            setOpen("mark-retried");
          }}
          disabled={isPending}
        >
          Đánh dấu thử lại
        </button>
        <button
          type="button"
          className="btn btn-ghost btn-sm"
          onClick={() => {
            setError(null);
            setNote("");
            setOpen("dead-letter");
          }}
          disabled={isPending}
        >
          Dead letter
        </button>
      </div>

      {error && !open ? (
        <div
          className="alert alert-error"
          role="alert"
          style={{ marginTop: "0.35rem" }}
        >
          {error}
        </div>
      ) : null}

      {open ? (
        <div
          className="dialog-backdrop"
          role="presentation"
          onClick={(e) => {
            if (e.target === e.currentTarget) close();
          }}
        >
          <div
            className="dialog"
            role="dialog"
            aria-modal="true"
            aria-labelledby={dialogTitleId}
          >
            <h2 id={dialogTitleId}>
              {open === "mark-retried"
                ? "Đánh dấu đã thử lại?"
                : "Chuyển sang dead letter?"}
            </h2>
            <p>
              {open === "mark-retried" ? (
                <>
                  Ghi nhận lỗi đã được thử xử lý lại. Trạng thái →{" "}
                  <strong>Đã thử lại</strong>.
                </>
              ) : (
                <>
                  Dừng thử lại tự động; chuyển bản ghi sang{" "}
                  <strong>dead letter</strong> để xử lý thủ công.
                </>
              )}
            </p>
            <div className="field">
              <label htmlFor={`int-note-${errorId}`}>Ghi chú (tuỳ chọn)</label>
              <input
                id={`int-note-${errorId}`}
                type="text"
                value={note}
                onChange={(e) => setNote(e.target.value)}
                maxLength={2048}
                disabled={submitting}
                placeholder="Ví dụ: đã đồng bộ lại từ TMS"
              />
            </div>
            {error ? (
              <div className="alert alert-error" role="alert">
                {error}
              </div>
            ) : null}
            <div className="dialog-actions">
              <button
                type="button"
                className="btn btn-ghost"
                onClick={close}
                disabled={submitting}
              >
                Hủy
              </button>
              <button
                type="button"
                className="btn"
                onClick={runAction}
                disabled={submitting}
              >
                {submitting ? "Đang gửi…" : "Xác nhận"}
              </button>
            </div>
          </div>
        </div>
      ) : null}
    </>
  );
}
