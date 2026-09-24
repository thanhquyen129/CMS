"use client";

import type { FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { term, type TerminologyMap } from "@/lib/terminology";
import { formatMoney } from "@/lib/money";
import { withIdempotency } from "@/lib/idempotency";
import { useIdempotency } from "@/lib/use-idempotency";
import { formatHttpError, readApiErrorBody } from "@/lib/api-error";

export type AllocateTargetOption = {
  id: string;
  label: string;
  outstanding: number;
  currencyCode: string;
};

type Kind = "payment" | "collection";

type Props = {
  terms: TerminologyMap;
  kind: Kind;
  cashId: string;
  availableToAllocate: number;
  currencyCode: string;
  targets: AllocateTargetOption[];
};

export function AllocateCashForm({
  terms,
  kind,
  cashId,
  availableToAllocate,
  currencyCode,
  targets,
}: Props) {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [isPending, startTransition] = useTransition();

  const isPayment = kind === "payment";
  const idem = useIdempotency(isPayment ? "pay-alloc" : "coll-alloc");
  const allocLabel = isPayment
    ? term(terms, "PAYMENT_ALLOCATION", "Phân bổ thanh toán")
    : term(terms, "COLLECTION_ALLOCATION", "Phân bổ thu tiền");
  const draftLabel = term(terms, "ALLOCATION_DRAFT", "Phân bổ nháp");
  const apLabel = term(terms, "ACCOUNTS_PAYABLE", "Khoản phải trả");
  const arLabel = term(terms, "ACCOUNTS_RECEIVABLE", "Khoản phải thu");
  const availableLabel = term(
    terms,
    "AVAILABLE_TO_ALLOCATE",
    "Còn phân bổ được"
  );

  if (availableToAllocate <= 0) {
    return (
      <p className="note">
        {availableLabel}: {formatMoney(0, currencyCode)}. Không còn số để phân
        bổ (đã phân bổ hết hoặc đã áp dụng).
      </p>
    );
  }

  if (targets.length === 0) {
    return (
      <div className="empty-state" role="status">
        Không có {isPayment ? apLabel : arLabel} còn dư để phân bổ. Ghi nhận
        AP/AR trước.
      </div>
    );
  }

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const idemKey = idem.acquire();
    if (!idemKey) return;
    setError(null);
    setSubmitting(true);
    let succeeded = false;

    const fd = new FormData(e.currentTarget);
    const targetId = String(fd.get("targetId") ?? "").trim();
    const amountRaw = String(fd.get("amount") ?? "").trim();
    const amount = Number(amountRaw.replace(",", "."));
    if (!targetId) {
      setError(`Chọn ${isPayment ? apLabel : arLabel}.`);
      setSubmitting(false);
      idem.release(false);
      return;
    }
    if (!Number.isFinite(amount) || amount <= 0) {
      setError("Số phân bổ phải lớn hơn 0.");
      setSubmitting(false);
      idem.release(false);
      return;
    }
    if (amount > availableToAllocate + 1e-9) {
      setError(
        `Số phân bổ vượt ${availableLabel.toLowerCase()} (${formatMoney(availableToAllocate, currencyCode)}).`
      );
      setSubmitting(false);
      idem.release(false);
      return;
    }

    const body = isPayment
      ? {
          accountsPayableId: targetId,
          amount,
          notes: String(fd.get("notes") ?? "").trim() || null,
        }
      : {
          accountsReceivableId: targetId,
          amount,
          notes: String(fd.get("notes") ?? "").trim() || null,
        };

    const endpoint = isPayment
      ? `/bff/payments/${cashId}/allocations`
      : `/bff/collections/${cashId}/allocations`;

    try {
      const res = await fetch(endpoint, {
        method: "POST",
        headers: withIdempotency(
          { "Content-Type": "application/json" },
          idemKey
        ),
        body: JSON.stringify(body),
      });

      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }

      if (!res.ok) {
        const payload = await readApiErrorBody(res);
        setError(
          formatHttpError(res.status, payload, {
            conflict: "Không phân bổ được (vượt số dư hoặc trạng thái lệch). Tải lại và thử lại.",
            default: "Phân bổ thất bại.",
          })
        );
        return;
      }

      succeeded = true;
      startTransition(() => router.refresh());
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      idem.release(succeeded);
      setSubmitting(false);
    }
  }

  const busy = submitting || isPending;

  return (
    <form className="receive-form" onSubmit={onSubmit} noValidate>
      <p className="note">
        Tạo {allocLabel.toLowerCase()} ở trạng thái <strong>{draftLabel}</strong>
        . Outstanding AP/AR chỉ đổi sau khi chốt phân bổ.
      </p>
      <p className="note">
        {availableLabel}:{" "}
        <strong>{formatMoney(availableToAllocate, currencyCode)}</strong>
      </p>

      {error ? (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      ) : null}

      <div className="form-grid">
        <div className="field field-span">
          <label htmlFor="targetId">
            {isPayment ? apLabel : arLabel} còn dư
          </label>
          <select id="targetId" name="targetId" required disabled={busy}>
            <option value="">— Chọn —</option>
            {targets.map((t) => (
              <option key={t.id} value={t.id}>
                {t.label} · còn {formatMoney(t.outstanding, t.currencyCode)}
              </option>
            ))}
          </select>
        </div>
        <div className="field">
          <label htmlFor="amount">Số phân bổ</label>
          <input
            id="amount"
            name="amount"
            type="number"
            inputMode="decimal"
            min={0}
            step="any"
            required
            disabled={busy}
            defaultValue={availableToAllocate}
          />
        </div>
        <div className="field field-span">
          <label htmlFor="notes">Ghi chú</label>
          <input id="notes" name="notes" maxLength={2048} disabled={busy} />
        </div>
      </div>

      <div className="cta-row">
        <button type="submit" className="btn btn-ghost" disabled={busy}>
          {busy ? "Đang phân bổ…" : `Tạo ${draftLabel.toLowerCase()}`}
        </button>
      </div>
    </form>
  );
}
