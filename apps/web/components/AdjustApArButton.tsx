"use client";

import { useRouter } from "next/navigation";
import { useId, useRef, useState, useTransition } from "react";
import { formatApiErrorMessage } from "@/lib/api-error";
import { newIdempotencyKey, withIdempotency, withRowVersion } from "@/lib/idempotency";

type Kind = "payable" | "receivable";

type Props = {
  kind: Kind;
  accountsId: string;
  currencyCode: string;
  outstanding: number;
  rowVersion?: string | null;
};

export function AdjustApArButton({
  kind,
  accountsId,
  currencyCode,
  outstanding,
  rowVersion,
}: Props) {
  const router = useRouter();
  const dialogTitleId = useId();
  const keyRef = useRef(newIdempotencyKey("apar-adj"));
  const [open, setOpen] = useState(false);
  const [delta, setDelta] = useState("");
  const [reason, setReason] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [isPending, startTransition] = useTransition();
  const label = kind === "payable" ? "phải trả" : "phải thu";

  async function submit() {
    const parsed = Number(String(delta).replace(",", "."));
    if (!Number.isFinite(parsed) || parsed === 0) {
      setError("Số điều chỉnh khác 0. Âm là giảm outstanding.");
      return;
    }
    const trimmed = reason.trim();
    if (!trimmed) {
      setError("Nhập lý do điều chỉnh.");
      return;
    }
    setBusy(true);
    setError(null);
    const path =
      kind === "payable"
        ? `/bff/accounts-payable/${accountsId}/adjust`
        : `/bff/accounts-receivable/${accountsId}/adjust`;
    try {
      const res = await fetch(path, {
        method: "POST",
        headers: withIdempotency(
          withRowVersion(
            { "Content-Type": "application/json", Accept: "application/json" },
            rowVersion
          ),
          keyRef.current
        ),
        body: JSON.stringify({ deltaAmount: parsed, reason: trimmed }),
      });
      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }
      if (!res.ok && res.status !== 201 && res.status !== 204) {
        const body = (await res.json().catch(() => ({}))) as {
          message?: string;
          errors?: Record<string, string[]>;
        };
        setError(formatApiErrorMessage(body, "Không điều chỉnh được."));
        return;
      }
      keyRef.current = newIdempotencyKey("apar-adj");
      setOpen(false);
      setDelta("");
      setReason("");
      startTransition(() => router.refresh());
    } catch {
      setError("Không kết nối được máy chủ.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div>
      <button type="button" className="btn btn-ghost btn-sm" onClick={() => setOpen(true)}>
        Điều chỉnh
      </button>
      {open ? (
        <form
          className="stack-form"
          onSubmit={(e) => {
            e.preventDefault();
            void submit();
          }}
        >
          <h2 id={dialogTitleId}>Điều chỉnh khoản {label}</h2>
          <p className="note">
            Outstanding hiện {outstanding} {currencyCode}. Điều chỉnh không phải xóa nợ và không phải thanh toán.
          </p>
          <label>
            Số điều chỉnh (+/−)
            <input
              inputMode="decimal"
              value={delta}
              onChange={(e) => setDelta(e.target.value)}
              required
            />
          </label>
          <label>
            Lý do
            <textarea value={reason} onChange={(e) => setReason(e.target.value)} maxLength={1024} required rows={3} />
          </label>
          {error ? (
            <div className="alert alert-error" role="alert">
              {error}
            </div>
          ) : null}
          <div className="cta-row">
            <button type="submit" className="btn btn-sm" disabled={busy || isPending}>
              {busy ? "Đang ghi…" : "Ghi điều chỉnh"}
            </button>
            <button type="button" className="btn btn-ghost btn-sm" disabled={busy} onClick={() => setOpen(false)}>
              Đóng
            </button>
          </div>
        </form>
      ) : null}
    </div>
  );
}
