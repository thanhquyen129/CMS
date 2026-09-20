"use client";

import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import type { BusinessParty } from "@/lib/party";

export function PartyBlockActions({ party }: { party: BusinessParty }) {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [isPending, startTransition] = useTransition();
  const [reason, setReason] = useState("");

  async function block() {
    if (reason.trim().length < 3) {
      setError("Nhập lý do chặn (ít nhất 3 ký tự).");
      return;
    }
    setBusy(true);
    setError(null);
    try {
      const res = await fetch(`/bff/admin/parties/${party.id}/block`, {
        method: "POST",
        headers: { "Content-Type": "application/json", Accept: "application/json" },
        body: JSON.stringify({ reason: reason.trim() }),
      });
      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }
      if (!res.ok) {
        const payload = (await res.json().catch(() => ({}))) as { message?: string };
        setError(payload.message || "Chặn đối tác thất bại.");
        return;
      }
      startTransition(() => router.refresh());
    } finally {
      setBusy(false);
    }
  }

  async function unblock() {
    setBusy(true);
    setError(null);
    try {
      const res = await fetch(`/bff/admin/parties/${party.id}/unblock`, {
        method: "POST",
        headers: { "Content-Type": "application/json", Accept: "application/json" },
        body: JSON.stringify({ reason: "Mở chặn" }),
      });
      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }
      if (!res.ok) {
        const payload = (await res.json().catch(() => ({}))) as { message?: string };
        setError(payload.message || "Mở chặn thất bại.");
        return;
      }
      startTransition(() => router.refresh());
    } finally {
      setBusy(false);
    }
  }

  const waiting = busy || isPending;

  return (
    <fieldset className="group-box">
      <legend>Chặn giao dịch</legend>
      {party.isBlocked ? (
        <>
          <p className="alert alert-error" role="status">
            Đang chặn. {party.blockedReason ? `Lý do: ${party.blockedReason}` : ""} Không gắn
            được lên Bill / chi phí / chứng từ mới.
          </p>
          <button type="button" className="btn" disabled={waiting} onClick={() => void unblock()}>
            {waiting ? "Đang xử lý…" : "Mở chặn"}
          </button>
        </>
      ) : (
        <>
          <p className="muted" style={{ marginTop: 0 }}>
            Chặn là giữ hồ sơ nhưng cấm phát sinh mới — khác với ngừng dùng.
          </p>
          <div className="field">
            <label htmlFor="blockReason">Lý do chặn</label>
            <input
              id="blockReason"
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              maxLength={512}
              disabled={waiting}
              placeholder="VD: vượt hạn mức, tranh chấp chứng từ"
            />
          </div>
          <button
            type="button"
            className="btn btn-ghost"
            disabled={waiting}
            onClick={() => void block()}
          >
            {waiting ? "Đang xử lý…" : "Chặn giao dịch"}
          </button>
        </>
      )}
      {error ? (
        <div className="alert alert-error" role="alert" style={{ marginTop: "0.75rem" }}>
          {error}
        </div>
      ) : null}
    </fieldset>
  );
}
