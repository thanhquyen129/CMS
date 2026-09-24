"use client";

import type { FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { BillTypeahead } from "@/components/BillTypeahead";
import { CurrencySelect } from "@/components/CurrencySelect";
import { formatApiErrorMessage } from "@/lib/api-error";

type Props = {
  documentId: string;
  currencyCode: string;
  billId?: string | null;
  billNo?: string | null;
  billLabel: string;
};

export function CorrectDocumentHeaderForm({
  documentId,
  currencyCode,
  billId,
  billNo,
  billLabel,
}: Props) {
  const router = useRouter();
  const [open, setOpen] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [isPending, startTransition] = useTransition();

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const fd = new FormData(e.currentTarget);
    const reason = String(fd.get("reason") ?? "").trim();
    if (!reason) {
      setError("Nhập lý do sửa.");
      return;
    }
    setBusy(true);
    setError(null);
    try {
      const res = await fetch(
        `/bff/financial-documents/${documentId}/correct-header`,
        {
          method: "POST",
          headers: { "Content-Type": "application/json", Accept: "application/json" },
          body: JSON.stringify({
            reason,
            currencyCode: String(fd.get("currencyCode") ?? "").trim() || null,
            billId: String(fd.get("billId") ?? "").trim() || null,
          }),
        }
      );
      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }
      if (!res.ok && res.status !== 204) {
        const body = (await res.json().catch(() => ({}))) as {
          message?: string;
          errors?: Record<string, string[]>;
        };
        setError(formatApiErrorMessage(body, "Sửa header thất bại."));
        return;
      }
      setOpen(false);
      startTransition(() => router.refresh());
    } catch {
      setError("Không kết nối được máy chủ.");
    } finally {
      setBusy(false);
    }
  }

  if (!open) {
    return (
      <button type="button" className="btn" disabled={isPending} onClick={() => setOpen(true)}>
        Sửa header
      </button>
    );
  }

  return (
    <form className="receive-form" onSubmit={onSubmit}>
      <p className="note">
        Chỉ khi đã nhận, chưa chấp nhận, chưa khớp. Có lý do và audit. Không ghi đè im lặng.
      </p>
      {error ? (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      ) : null}
      <div className="form-grid">
        <CurrencySelect id="correct-currency" defaultValue={currencyCode} disabled={busy} />
        <BillTypeahead
          label={billLabel}
          disabled={busy}
          defaultId={billId}
          defaultLabel={billNo}
        />
        <div className="field field-span">
          <label htmlFor="correct-reason">Lý do</label>
          <input id="correct-reason" name="reason" required maxLength={512} disabled={busy} />
        </div>
      </div>
      <div className="cta-row">
        <button className="btn" type="submit" disabled={busy}>
          {busy ? "Đang lưu…" : "Lưu sửa header"}
        </button>
        <button
          type="button"
          className="btn btn-ghost"
          disabled={busy}
          onClick={() => setOpen(false)}
        >
          Đóng
        </button>
      </div>
    </form>
  );
}
