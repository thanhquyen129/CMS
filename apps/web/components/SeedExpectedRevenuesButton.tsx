"use client";

import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { term, type TerminologyMap } from "@/lib/terminology";

type Props = {
  terms: TerminologyMap;
  ratingId: string;
};

export function SeedExpectedRevenuesButton({ terms, ratingId }: Props) {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [info, setInfo] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [isPending, startTransition] = useTransition();

  const expected = term(terms, "EXPECTED", "Dự kiến");
  const revenueLabel = term(terms, "REVENUE", "Doanh thu");

  async function onSeed() {
    setError(null);
    setInfo(null);
    setBusy(true);
    try {
      const res = await fetch(
        `/bff/ratings/${ratingId}/seed-expected-revenues`,
        { method: "POST", headers: { Accept: "application/json" } }
      );
      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }
      if (!res.ok) {
        const payload = (await res.json().catch(() => ({}))) as {
          message?: string;
        };
        setError(
          payload.message ||
            (res.status === 409
              ? "Không có dòng doanh thu trên lần tính giá (cần thành phần tính chất Doanh thu)."
              : "Seed thất bại.")
        );
        return;
      }
      const result = (await res.json().catch(() => ({}))) as {
        createdCount?: number;
        existingCount?: number;
      };
      const created = result.createdCount ?? 0;
      const existing = result.existingCount ?? 0;
      setInfo(
        created > 0
          ? `Đã tạo ${created} ${revenueLabel.toLowerCase()} ${expected.toLowerCase()}${
              existing > 0 ? ` (${existing} đã có sẵn).` : "."
            }`
          : `Đã seed trước đó (${existing} dòng). Không tạo trùng.`
      );
      startTransition(() => router.refresh());
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      setBusy(false);
    }
  }

  const loading = busy || isPending;

  return (
    <div>
      {error ? (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      ) : null}
      {info ? (
        <div className="alert alert-info" role="status">
          {info}
        </div>
      ) : null}
      <button
        type="button"
        className="btn btn-ghost btn-sm"
        disabled={loading}
        onClick={onSeed}
      >
        {loading
          ? "Đang seed…"
          : `Seed ${revenueLabel.toLowerCase()} ${expected.toLowerCase()}`}
      </button>
    </div>
  );
}
