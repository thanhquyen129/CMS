"use client";

import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { formatApiErrorMessage } from "@/lib/api-error";
import type { RateCard } from "@/lib/rate-cards";

type Props = { card: RateCard; inline?: boolean };

export function SetRateCardActiveButton({ card, inline = false }: Props) {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [isPending, startTransition] = useTransition();
  const nextActive = !card.isActive;

  async function toggle() {
    const verb = nextActive ? "Dùng lại" : "Ngừng sử dụng";
    if (!window.confirm(`${verb} bảng giá ${card.code}? Phiên bản đã phát hành được giữ nguyên.`)) {
      return;
    }
    setBusy(true);
    setError(null);
    try {
      const res = await fetch(`/bff/rate-cards/${card.id}`, {
        method: "PUT",
        headers: { Accept: "application/json", "Content-Type": "application/json" },
        body: JSON.stringify({
          name: card.name,
          partyType: card.partyType,
          currencyCode: card.currencyCode,
          description: card.description,
          isActive: nextActive,
          transportMode: card.transportMode ?? null,
          routeCode: card.routeCode ?? null,
          carrierName: card.carrierName ?? null,
        }),
      });
      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }
      if (!res.ok && res.status !== 204) {
        const body = (await res.json().catch(() => ({}))) as {
          message?: string;
          errors?: Record<string, string[]>;
        };
        setError(formatApiErrorMessage(body, "Không cập nhật được trạng thái bảng giá."));
        return;
      }
      startTransition(() => router.refresh());
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      setBusy(false);
    }
  }

  const body = (
    <>
      {error ? (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      ) : null}
      <button className="btn btn-ghost" type="button" onClick={toggle} disabled={busy || isPending}>
        {busy || isPending ? "Đang lưu…" : nextActive ? "Dùng lại" : "Ngừng sử dụng"}
      </button>
    </>
  );
  if (inline) return body;
  return <div className="cta-row">{body}</div>;
}
