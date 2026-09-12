"use client";

import { useRouter } from "next/navigation";
import { useCallback, useId, useState, useTransition } from "react";
import { term, type TerminologyMap } from "@/lib/terminology";

type Decision = "approve" | "reject";

type Props = {
  terms: TerminologyMap;
  approvalId: string;
  currentLevel: number;
  requiredLevel: number;
};

export function DecideApprovalButton({
  terms,
  approvalId,
  currentLevel,
  requiredLevel,
}: Props) {
  const router = useRouter();
  const dialogTitleId = useId();
  const [open, setOpen] = useState<Decision | null>(null);
  const [reason, setReason] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [isPending, startTransition] = useTransition();

  const approveLabel = term(terms, "APPROVAL_APPROVED", "Đã phê duyệt");
  const rejectLabel = term(terms, "APPROVAL_REJECTED", "Từ chối phê duyệt");
  const stepsLeft = Math.max(0, requiredLevel - currentLevel);
  const isFinalApprove = stepsLeft <= 1;

  const close = useCallback(() => {
    if (submitting) return;
    setOpen(null);
  }, [submitting]);

  const runDecide = useCallback(async () => {
    if (!open) return;
    const trimmed = reason.trim();
    if (open === "reject" && !trimmed) {
      setError("Nhập lý do từ chối.");
      return;
    }

    setSubmitting(true);
    setError(null);
    const endpoint =
      open === "approve"
        ? `/bff/approvals/${approvalId}/approve`
        : `/bff/approvals/${approvalId}/reject`;

    try {
      const res = await fetch(endpoint, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Accept: "application/json",
        },
        body: JSON.stringify({
          decisionReason: trimmed || null,
        }),
      });

      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }

      if (!res.ok && res.status !== 204) {
        const body = (await res.json().catch(() => ({}))) as {
          message?: string;
        };
        setError(
          body.message ||
            (res.status === 409
              ? "Không quyết định được (đã xử lý / trạng thái lệch). Tải lại trang."
              : "Quyết định phê duyệt thất bại.")
        );
        return;
      }

      setOpen(null);
      setReason("");
      startTransition(() => router.refresh());
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      setSubmitting(false);
    }
  }, [approvalId, open, reason, router]);

  return (
    <>
      <div className="row-actions">
        <button
          type="button"
          className="btn btn-sm"
          onClick={() => {
            setError(null);
            setReason("");
            setOpen("approve");
          }}
          disabled={isPending}
        >
          Phê duyệt
        </button>
        <button
          type="button"
          className="btn btn-ghost btn-sm"
          onClick={() => {
            setError(null);
            setReason("");
            setOpen("reject");
          }}
          disabled={isPending}
        >
          Từ chối
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
              {open === "approve" ? "Phê duyệt?" : "Từ chối phê duyệt?"}
            </h2>
            <p>
              {open === "approve" ? (
                isFinalApprove ? (
                  <>
                    Đây là bước cuối (cấp {currentLevel + 1}/{requiredLevel}).
                    Trạng thái → <strong>{approveLabel}</strong>. Phê duyệt ≠
                    quyền hệ thống (Permission).
                  </>
                ) : (
                  <>
                    Tiến một cấp ({currentLevel} → {currentLevel + 1}/
                    {requiredLevel}). Vẫn chờ cấp tiếp theo — chưa{" "}
                    {approveLabel.toLowerCase()} cuối.
                  </>
                )
              ) : (
                <>
                  Trạng thái → <strong>{rejectLabel}</strong>. Lý do bắt buộc.
                  Không đổi ma trận quyền.
                </>
              )}
            </p>
            <div className="field">
              <label htmlFor={`dec-reason-${approvalId}`}>
                {open === "reject" ? "Lý do từ chối" : "Ghi chú (tuỳ chọn)"}
              </label>
              <input
                id={`dec-reason-${approvalId}`}
                type="text"
                value={reason}
                onChange={(e) => setReason(e.target.value)}
                maxLength={2048}
                disabled={submitting}
                required={open === "reject"}
                placeholder={
                  open === "reject"
                    ? "Ví dụ: thiếu chứng từ hỗ trợ"
                    : "Ví dụ: đã kiểm tra số tiền"
                }
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
                onClick={runDecide}
                disabled={submitting}
              >
                {submitting
                  ? "Đang gửi…"
                  : open === "approve"
                    ? "Xác nhận phê duyệt"
                    : "Xác nhận từ chối"}
              </button>
            </div>
          </div>
        </div>
      ) : null}
    </>
  );
}
