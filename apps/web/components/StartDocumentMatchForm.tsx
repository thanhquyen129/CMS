"use client";

import type { FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import {
  MATCH_METHODS,
  matchMethodLabel,
  type MatchMethod,
} from "@/lib/document-matches";
import { withIdempotency } from "@/lib/idempotency";
import { useIdempotency } from "@/lib/use-idempotency";
import { term, type TerminologyMap } from "@/lib/terminology";

type Props = {
  terms: TerminologyMap;
  documentId: string;
  documentNo: string;
  defaultMethod: MatchMethod;
};

export function StartDocumentMatchForm({
  terms,
  documentId,
  documentNo,
  defaultMethod,
}: Props) {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [isPending, startTransition] = useTransition();
  const idem = useIdempotency("doc-match-start");

  const matchLabel = term(terms, "MATCHED", "Khớp");
  const costLabel = term(terms, "COST", "Chi phí");
  const revenueLabel = term(terms, "REVENUE", "Doanh thu");

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const idemKey = idem.acquire();
    if (!idemKey) return;
    setError(null);
    setSubmitting(true);
    let succeeded = false;

    const fd = new FormData(e.currentTarget);
    const matchMethod = String(fd.get("matchMethod") ?? "").trim();
    const notes = String(fd.get("notes") ?? "").trim() || null;

    if (!MATCH_METHODS.includes(matchMethod as MatchMethod)) {
      setError("Chọn phương thức khớp hợp lệ.");
      setSubmitting(false);
      idem.release(false);
      return;
    }

    try {
      const res = await fetch("/bff/document-matches", {
        method: "POST",
        headers: withIdempotency(
          { "Content-Type": "application/json" },
          idemKey
        ),
        body: JSON.stringify({
          primaryDocumentId: documentId,
          matchMethod,
          notes,
        }),
      });

      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }

      const body = (await res.json().catch(() => ({}))) as {
        id?: string;
        message?: string;
      };

      if (!res.ok) {
        setError(
          body.message ||
            (res.status === 409
              ? "Không mở phiên khớp được (chưa chấp nhận / trạng thái lệch)."
              : "Mở phiên khớp thất bại.")
        );
        return;
      }

      if (!body.id) {
        setError("Máy chủ không trả mã phiên khớp.");
        return;
      }

      succeeded = true;
      startTransition(() => {
        router.push(`/documents/${documentId}/matches/${body.id}`);
        router.refresh();
      });
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      idem.release(succeeded);
      setSubmitting(false);
    }
  }

  return (
    <form className="receive-form" onSubmit={onSubmit}>
      <p className="note">
        Mở phiên {matchLabel.toLowerCase()} cho số {documentNo}. Chỉ{" "}
        <strong>liên kết</strong> dòng chứng từ với {costLabel.toLowerCase()} /{" "}
        {revenueLabel.toLowerCase()} / dòng khác — không tạo chi phí hay doanh
        thu mới.
      </p>

      <div className="form-grid">
        <div className="field field-span">
          <label htmlFor="matchMethod">Phương thức khớp</label>
          <select
            id="matchMethod"
            name="matchMethod"
            defaultValue={defaultMethod}
            required
            disabled={submitting || isPending}
          >
            {MATCH_METHODS.map((m) => (
              <option key={m} value={m}>
                {matchMethodLabel(terms, m)}
              </option>
            ))}
          </select>
        </div>
        <div className="field field-span">
          <label htmlFor="notes">Ghi chú (tuỳ chọn)</label>
          <input
            id="notes"
            name="notes"
            type="text"
            maxLength={1024}
            disabled={submitting || isPending}
            placeholder="Ví dụ: khớp DN tháng 9"
          />
        </div>
      </div>

      {error ? (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      ) : null}

      <div className="cta-row">
        <button
          type="submit"
          className="btn"
          disabled={submitting || isPending}
        >
          {submitting || isPending
            ? "Đang mở phiên…"
            : `Mở phiên ${matchLabel.toLowerCase()}`}
        </button>
      </div>
    </form>
  );
}
