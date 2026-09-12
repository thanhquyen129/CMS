"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useCallback, useId, useState, useTransition } from "react";
import { formatMoney } from "@/lib/money";
import { term, type TerminologyMap } from "@/lib/terminology";

type Kind = "payable" | "receivable";

/** Matches Settlement:MaxWriteOffAmount — apply immediately under this; above → Approval (P03). */
export const MAX_WRITE_OFF_IMMEDIATE = 1000;

type Props = {
  terms: TerminologyMap;
  kind: Kind;
  accountsId: string;
  outstanding: number;
  currencyCode: string;
};

export function WriteOffButton({
  terms,
  kind,
  accountsId,
  outstanding,
  currencyCode,
}: Props) {
  const router = useRouter();
  const dialogTitleId = useId();
  const [open, setOpen] = useState(false);
  const [amount, setAmount] = useState("");
  const [reason, setReason] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [info, setInfo] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [isPending, startTransition] = useTransition();

  const writeOffLabel = term(terms, "WRITE_OFF", "Xóa nợ / write-off");
  const apLabel = term(terms, "ACCOUNTS_PAYABLE", "Khoản phải trả");
  const arLabel = term(terms, "ACCOUNTS_RECEIVABLE", "Khoản phải thu");
  const target = kind === "payable" ? apLabel : arLabel;
  const paymentLabel = term(terms, "PAYMENT", "Thanh toán");
  const collectionLabel = term(terms, "COLLECTION", "Thu tiền");
  const cashLabel = kind === "payable" ? paymentLabel : collectionLabel;
  const approvalQueueLabel = term(terms, "APPROVAL_QUEUE", "Hàng đợi phê duyệt");

  const defaultAmount = Math.min(outstanding, MAX_WRITE_OFF_IMMEDIATE);

  const close = useCallback(() => {
    if (submitting) return;
    setOpen(false);
  }, [submitting]);

  const runWriteOff = useCallback(async () => {
    const parsed = Number(String(amount).replace(",", "."));
    if (!Number.isFinite(parsed) || parsed <= 0) {
      setError("Số tiền xóa nợ phải lớn hơn 0.");
      return;
    }
    if (parsed > outstanding + 0.0000001) {
      setError(
        `Số tiền xóa nợ vượt số dư còn lại (${formatMoney(outstanding, currencyCode)}).`
      );
      return;
    }
    const trimmed = reason.trim();
    if (!trimmed) {
      setError("Nhập lý do xóa nợ.");
      return;
    }

    setSubmitting(true);
    setError(null);
    setInfo(null);
    const endpoint =
      kind === "payable"
        ? `/bff/accounts-payable/${accountsId}/write-off`
        : `/bff/accounts-receivable/${accountsId}/write-off`;

    try {
      const res = await fetch(endpoint, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Accept: "application/json",
        },
        body: JSON.stringify({ amount: parsed, reason: trimmed }),
      });

      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }

      if (res.status === 202) {
        const body = (await res.json().catch(() => ({}))) as {
          message?: string;
          approvalId?: string;
        };
        setOpen(false);
        setAmount("");
        setReason("");
        setInfo(
          body.message ||
            `Đã gửi phê duyệt xóa nợ (vượt trần ${formatMoney(MAX_WRITE_OFF_IMMEDIATE, currencyCode)}). Số dư chưa đổi cho đến khi duyệt.`
        );
        startTransition(() => router.refresh());
        return;
      }

      if (!res.ok && res.status !== 204) {
        const body = (await res.json().catch(() => ({}))) as {
          message?: string;
        };
        setError(
          body.message ||
            (res.status === 409
              ? "Không xóa nợ được (số dư / trạng thái / đang chờ duyệt). Tải lại trang."
              : "Xóa nợ thất bại.")
        );
        return;
      }

      setOpen(false);
      setAmount("");
      setReason("");
      setInfo(null);
      startTransition(() => router.refresh());
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      setSubmitting(false);
    }
  }, [accountsId, amount, currencyCode, kind, outstanding, reason, router]);

  if (outstanding <= 0) return null;

  return (
    <>
      <button
        type="button"
        className="btn btn-ghost btn-sm"
        onClick={() => {
          setError(null);
          setInfo(null);
          setAmount(String(defaultAmount));
          setOpen(true);
        }}
        disabled={isPending}
      >
        {writeOffLabel}
      </button>

      {info && !open ? (
        <div
          className="alert alert-info"
          role="status"
          style={{ marginTop: "0.35rem" }}
        >
          {info}{" "}
          <Link className="row-link" href="/queues/approvals">
            Mở {approvalQueueLabel.toLowerCase()}
          </Link>
        </div>
      ) : null}

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
            <h2 id={dialogTitleId}>{writeOffLabel}?</h2>
            <p>
              Giảm nghĩa vụ {target} bằng điều chỉnh (không tạo {cashLabel},
              không giả tất toán tiền mặt). Số dư:{" "}
              {formatMoney(outstanding, currencyCode)}. Trần áp dụng ngay:{" "}
              {formatMoney(MAX_WRITE_OFF_IMMEDIATE, currencyCode)} — vượt trần
              sẽ gửi {approvalQueueLabel.toLowerCase()}, chỉ ghi nợ khi được
              duyệt.
            </p>
            <div className="field">
              <label htmlFor={`wo-amt-${accountsId}`}>Số tiền xóa nợ</label>
              <input
                id={`wo-amt-${accountsId}`}
                type="number"
                inputMode="decimal"
                step="any"
                min="0"
                max={outstanding}
                value={amount}
                onChange={(e) => setAmount(e.target.value)}
                disabled={submitting}
                required
              />
            </div>
            <div className="field">
              <label htmlFor={`wo-reason-${accountsId}`}>Lý do</label>
              <input
                id={`wo-reason-${accountsId}`}
                type="text"
                value={reason}
                onChange={(e) => setReason(e.target.value)}
                maxLength={1024}
                disabled={submitting}
                required
                placeholder="Ví dụ: chênh lệch làm tròn / miễn thỏa thuận"
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
                onClick={runWriteOff}
                disabled={submitting}
              >
                {submitting ? "Đang gửi…" : "Xác nhận xóa nợ"}
              </button>
            </div>
          </div>
        </div>
      ) : null}
    </>
  );
}
