"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useCallback, useId, useState, useTransition } from "react";
import { withRowVersion } from "@/lib/idempotency";
import { formatMoney } from "@/lib/money";
import { term, type TerminologyMap } from "@/lib/terminology";
import { BalanceImpact } from "./BalanceImpact";

type Kind = "payable" | "receivable";

type Props = {
  terms: TerminologyMap;
  kind: Kind;
  accountsId: string;
  outstanding: number;
  currencyCode: string;
  rowVersion?: string | null;
};

export function WriteOffButton({
  terms,
  kind,
  accountsId,
  outstanding,
  currencyCode,
  rowVersion,
}: Props) {
  const router = useRouter();
  const dialogTitleId = useId();
  const [open, setOpen] = useState(false);
  const [step, setStep] = useState<1 | 2>(1);
  const [amount, setAmount] = useState("");
  const [reason, setReason] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [info, setInfo] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [isPending, startTransition] = useTransition();

  const writeOffLabel = term(terms, "WRITE_OFF", "Xóa nợ");
  const apLabel = term(terms, "ACCOUNTS_PAYABLE", "Khoản phải trả");
  const arLabel = term(terms, "ACCOUNTS_RECEIVABLE", "Khoản phải thu");
  const target = kind === "payable" ? apLabel : arLabel;
  const paymentLabel = term(terms, "PAYMENT", "Thanh toán");
  const collectionLabel = term(terms, "COLLECTION", "Thu tiền");
  const cashLabel = kind === "payable" ? paymentLabel : collectionLabel;
  const approvalQueueLabel = term(terms, "APPROVAL_QUEUE", "Hàng đợi phê duyệt");

  const parsedPreview = Number(String(amount).replace(",", "."));

  const close = useCallback(() => {
    if (submitting) return;
    setOpen(false);
    setStep(1);
  }, [submitting]);

  const goStep2 = useCallback(() => {
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
    setError(null);
    setStep(2);
  }, [amount, currencyCode, outstanding]);

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
        headers: withRowVersion(
          {
            "Content-Type": "application/json",
            Accept: "application/json",
          },
          rowVersion
        ),
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
          requiredLevel?: number;
        };
        const levelNote =
          body.requiredLevel != null
            ? ` Cấp phê duyệt yêu cầu: ${body.requiredLevel}.`
            : "";
        setOpen(false);
        setStep(1);
        setAmount("");
        setReason("");
        setInfo(
          (body.message ||
            "Đã gửi phê duyệt xóa nợ theo hạn mức của đơn vị. Số dư chưa đổi cho đến khi duyệt.") +
            levelNote
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
      setStep(1);
      setAmount("");
      setReason("");
      setInfo(null);
      startTransition(() => router.refresh());
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      setSubmitting(false);
    }
  }, [accountsId, amount, currencyCode, kind, outstanding, reason, router, rowVersion]);

  const approvalNote =
    "Trong hạn mức xóa nợ của đơn vị: áp dụng ngay. Vượt hạn mức: hệ thống gửi phê duyệt, số dư chỉ đổi khi được duyệt.";

  if (outstanding <= 0) return null;

  return (
    <>
      <button
        type="button"
        className="btn btn-ghost btn-sm"
        onClick={() => {
          setError(null);
          setInfo(null);
          setAmount(String(outstanding));
          setReason("");
          setStep(1);
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
            <h2 id={dialogTitleId}>
              {writeOffLabel}
              {step === 1 ? " — Bước 1/2" : " — Bước 2/2"}
            </h2>

            {step === 1 ? (
              <>
                <p>
                  Giảm nghĩa vụ {target} bằng điều chỉnh (không tạo {cashLabel},
                  không giả tất toán tiền mặt). Số dư:{" "}
                  {formatMoney(outstanding, currencyCode)}.
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
                <BalanceImpact
                  before={outstanding}
                  after={
                    Number.isFinite(parsedPreview) && parsedPreview > 0
                      ? outstanding - parsedPreview
                      : null
                  }
                  currencyCode={currencyCode}
                />
                <p className="muted">{approvalNote}</p>
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
                    onClick={goStep2}
                    disabled={submitting}
                  >
                    Tiếp theo
                  </button>
                </div>
              </>
            ) : (
              <>
                <p>
                  Xác nhận xóa nợ{" "}
                  <strong>
                    {formatMoney(parsedPreview, currencyCode)}
                  </strong>

                  . Lý do được lưu vĩnh viễn trong sổ công nợ; có thể hoàn tác
                  bằng bút toán bù, không sửa hoặc xóa bản ghi.
                </p>
                <BalanceImpact
                  before={outstanding}
                  after={outstanding - parsedPreview}
                  currencyCode={currencyCode}
                />
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
                    onClick={() => {
                      setError(null);
                      setStep(1);
                    }}
                    disabled={submitting}
                  >
                    Quay lại
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
              </>
            )}
          </div>
        </div>
      ) : null}
    </>
  );
}
