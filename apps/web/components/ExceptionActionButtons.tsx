"use client";

import { useRouter } from "next/navigation";
import { useCallback, useId, useState, useTransition } from "react";
import { term, type TerminologyMap } from "@/lib/terminology";

type Action = "resolve" | "close" | "escalate";

type Props = {
  terms: TerminologyMap;
  exceptionId: string;
  status: string;
};

export function ExceptionActionButtons({
  terms,
  exceptionId,
  status,
}: Props) {
  const router = useRouter();
  const dialogTitleId = useId();
  const [open, setOpen] = useState<Action | null>(null);
  const [notes, setNotes] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [isPending, startTransition] = useTransition();

  const resolvedLabel = term(terms, "EXCEPTION_RESOLVED", "Ngoại lệ đã xử lý");
  const closedLabel = term(terms, "EXCEPTION_CLOSED", "Ngoại lệ đã đóng");
  const escalatedLabel = term(
    terms,
    "EXCEPTION_ESCALATED",
    "Ngoại lệ đã leo thang"
  );

  const s = status?.toLowerCase() ?? "";
  const canResolve =
    s === "open" || s === "in_progress" || s === "escalated";
  const canEscalate = s === "open" || s === "in_progress";
  const canClose =
    s === "open" ||
    s === "in_progress" ||
    s === "escalated" ||
    s === "resolved";

  const close = useCallback(() => {
    if (submitting) return;
    setOpen(null);
  }, [submitting]);

  const runAction = useCallback(async () => {
    if (!open) return;
    const trimmed = notes.trim();
    if (open === "escalate" && !trimmed) {
      setError("Nhập lý do leo thang.");
      return;
    }

    setSubmitting(true);
    setError(null);

    const endpoint =
      open === "resolve"
        ? `/bff/exceptions/${exceptionId}/resolve`
        : open === "close"
          ? `/bff/exceptions/${exceptionId}/close`
          : `/bff/exceptions/${exceptionId}/escalate`;

    const body =
      open === "resolve"
        ? { resolutionNotes: trimmed || null }
        : open === "escalate"
          ? { escalationReason: trimmed }
          : undefined;

    try {
      const res = await fetch(endpoint, {
        method: "POST",
        headers: {
          Accept: "application/json",
          ...(body !== undefined
            ? { "Content-Type": "application/json" }
            : {}),
        },
        body: body !== undefined ? JSON.stringify(body) : undefined,
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
              ? "Không thao tác được (đã đóng / trạng thái lệch). Tải lại trang."
              : "Thao tác ngoại lệ thất bại.")
        );
        return;
      }

      setOpen(null);
      setNotes("");
      startTransition(() => router.refresh());
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      setSubmitting(false);
    }
  }, [exceptionId, notes, open, router]);

  if (!canResolve && !canEscalate && !canClose) {
    return <span className="muted">—</span>;
  }

  return (
    <>
      <div className="row-actions">
        {canResolve ? (
          <button
            type="button"
            className="btn btn-sm"
            onClick={() => {
              setError(null);
              setNotes("");
              setOpen("resolve");
            }}
            disabled={isPending}
          >
            Xử lý
          </button>
        ) : null}
        {canEscalate ? (
          <button
            type="button"
            className="btn btn-ghost btn-sm"
            onClick={() => {
              setError(null);
              setNotes("");
              setOpen("escalate");
            }}
            disabled={isPending}
          >
            Leo thang
          </button>
        ) : null}
        {canClose ? (
          <button
            type="button"
            className="btn btn-ghost btn-sm"
            onClick={() => {
              setError(null);
              setNotes("");
              setOpen("close");
            }}
            disabled={isPending}
          >
            Đóng
          </button>
        ) : null}
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
              {open === "resolve"
                ? "Xử lý ngoại lệ?"
                : open === "escalate"
                  ? "Leo thang ngoại lệ?"
                  : "Đóng ngoại lệ?"}
            </h2>
            <p>
              {open === "resolve" ? (
                <>
                  Trạng thái → <strong>{resolvedLabel}</strong>. Không xóa
                  cứng; không tự sửa Variance.
                </>
              ) : open === "escalate" ? (
                <>
                  Trạng thái → <strong>{escalatedLabel}</strong>. Lý do bắt
                  buộc. Mức độ có thể tăng một bậc.
                </>
              ) : (
                <>
                  Trạng thái → <strong>{closedLabel}</strong>. Không xóa cứng.
                </>
              )}
            </p>
            {open === "resolve" || open === "escalate" ? (
              <div className="field">
                <label htmlFor={`exc-notes-${exceptionId}`}>
                  {open === "escalate" ? "Lý do leo thang" : "Ghi chú xử lý"}
                </label>
                <input
                  id={`exc-notes-${exceptionId}`}
                  type="text"
                  value={notes}
                  onChange={(e) => setNotes(e.target.value)}
                  maxLength={2048}
                  disabled={submitting}
                  required={open === "escalate"}
                  placeholder={
                    open === "escalate"
                      ? "Ví dụ: vượt SLA / cần controller"
                      : "Ví dụ: đã đối chiếu với Bill"
                  }
                />
              </div>
            ) : null}
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
                {submitting
                  ? "Đang gửi…"
                  : open === "resolve"
                    ? "Xác nhận xử lý"
                    : open === "escalate"
                      ? "Xác nhận leo thang"
                      : "Xác nhận đóng"}
              </button>
            </div>
          </div>
        </div>
      ) : null}
    </>
  );
}
