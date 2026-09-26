"use client";

import type { FormEvent } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useMemo, useState, useTransition } from "react";
import { withRowVersion } from "@/lib/idempotency";
import { formatMoney } from "@/lib/money";
import { term, type TerminologyMap } from "@/lib/terminology";

export type MatchSourceLineOption = {
  id: string;
  label: string;
  openAmount: number;
  currencyCode: string;
};

export type MatchTargetOption = {
  id: string;
  label: string;
};

type Props = {
  terms: TerminologyMap;
  matchId: string;
  matchMethod: string;
  sourceLines: MatchSourceLineOption[];
  targets: MatchTargetOption[];
  /** When line_to_line and no remote doc loaded yet */
  targetDocHint?: string | null;
  /** Link back to add lines when no open source lines */
  documentId?: string;
  rowVersion?: string | null;
};

export function AddMatchDetailForm({
  terms,
  matchId,
  matchMethod,
  sourceLines,
  targets,
  targetDocHint,
  documentId,
  rowVersion,
}: Props) {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [isPending, startTransition] = useTransition();
  const [sourceLineId, setSourceLineId] = useState(sourceLines[0]?.id ?? "");

  const matchLabel = term(terms, "MATCHED", "Khớp");
  const costLabel = term(terms, "COST", "Chi phí");
  const revenueLabel = term(terms, "REVENUE", "Doanh thu");
  const lineLabel = term(terms, "FINANCIAL_DOCUMENT_LINE", "Dòng chứng từ");

  const selectedLine = useMemo(
    () => sourceLines.find((l) => l.id === sourceLineId),
    [sourceLines, sourceLineId]
  );

  const method = matchMethod?.toLowerCase() ?? "";
  const targetLabel =
    method === "line_to_cost"
      ? costLabel
      : method === "line_to_revenue"
        ? revenueLabel
        : lineLabel;

  if (sourceLines.length === 0) {
    return (
      <div className="empty-state" role="status">
        <p>
          Không còn dòng mở để khớp (open = 0). Thêm dòng trên chứng từ hoặc hủy khớp
          khớp trước.
        </p>
        {documentId ? (
          <p className="cta-row" style={{ marginTop: "0.75rem" }}>
            <Link className="btn btn-sm" href={`/documents/${documentId}`}>
              Thêm {lineLabel.toLowerCase()}
            </Link>
          </p>
        ) : null}
      </div>
    );
  }

  if (targets.length === 0) {
    return (
      <div className="empty-state" role="status">
        {method === "line_to_line"
          ? targetDocHint ||
            "Chọn chứng từ đích (đã chấp nhận) rồi tải lại để lấy dòng."
          : targetDocHint ||
            (method === "line_to_cost"
              ? `Không có ${costLabel.toLowerCase()} đủ điều kiện trên Bill (cùng tiền tệ, còn hiệu lực). Không tạo chi phí mới để khớp.`
              : `Không có ${revenueLabel.toLowerCase()} đủ điều kiện trên Bill. Không tạo doanh thu mới để khớp.`)}
      </div>
    );
  }

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setError(null);
    setSubmitting(true);

    const fd = new FormData(e.currentTarget);
    const source = String(fd.get("sourceLineId") ?? "").trim();
    const targetId = String(fd.get("targetId") ?? "").trim();
    const amountRaw = String(fd.get("matchedAmount") ?? "").trim();
    const amount = Number(amountRaw.replace(",", "."));

    if (!source) {
      setError(`Chọn ${lineLabel.toLowerCase()} nguồn.`);
      setSubmitting(false);
      return;
    }
    if (!targetId) {
      setError(`Chọn ${targetLabel.toLowerCase()} đích.`);
      setSubmitting(false);
      return;
    }
    if (!Number.isFinite(amount) || amount <= 0) {
      setError("Số tiền khớp phải lớn hơn 0.");
      setSubmitting(false);
      return;
    }

    const body: Record<string, unknown> = {
      sourceLineId: source,
      matchedAmount: amount,
      targetLineId: null,
      targetCostId: null,
      targetRevenueId: null,
    };
    if (method === "line_to_line") body.targetLineId = targetId;
    else if (method === "line_to_cost") body.targetCostId = targetId;
    else if (method === "line_to_revenue") body.targetRevenueId = targetId;

    try {
      const res = await fetch(`/bff/document-matches/${matchId}/details`, {
        method: "POST",
        headers: withRowVersion(
          {
            "Content-Type": "application/json",
            Accept: "application/json",
          },
          rowVersion
        ),
        body: JSON.stringify(body),
      });

      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }

      if (!res.ok) {
        const err = (await res.json().catch(() => ({}))) as {
          message?: string;
        };
        setError(
          err.message ||
            (res.status === 409
              ? "Không khớp được (vượt số mở / C-007 / trạng thái). Tải lại trang."
              : "Thêm chi tiết khớp thất bại.")
        );
        return;
      }

      (e.target as HTMLFormElement).reset();
      setSourceLineId(sourceLines[0]?.id ?? "");
      startTransition(() => router.refresh());
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <form className="receive-form" onSubmit={onSubmit}>
      <p className="note">
        Thêm một liên kết {matchLabel.toLowerCase()}. Không tạo{" "}
        {costLabel.toLowerCase()} / {revenueLabel.toLowerCase()} mới.
      </p>
      <div className="form-grid">
        <div className="field field-span">
          <label htmlFor="sourceLineId">{lineLabel} nguồn</label>
          <select
            id="sourceLineId"
            name="sourceLineId"
            value={sourceLineId}
            onChange={(ev) => setSourceLineId(ev.target.value)}
            required
            disabled={submitting || isPending}
          >
            {sourceLines.map((l) => (
              <option key={l.id} value={l.id}>
                {l.label}
              </option>
            ))}
          </select>
        </div>
        <div className="field field-span">
          <label htmlFor="targetId">{targetLabel} đích</label>
          <select
            id="targetId"
            name="targetId"
            required
            disabled={submitting || isPending}
          >
            <option value="">— Chọn —</option>
            {targets.map((t) => (
              <option key={t.id} value={t.id}>
                {t.label}
              </option>
            ))}
          </select>
        </div>
        <div className="field">
          <label htmlFor="matchedAmount">Số tiền khớp</label>
          <input
            id="matchedAmount"
            name="matchedAmount"
            type="number"
            step="any"
            min="0.01"
            required
            disabled={submitting || isPending}
            defaultValue={
              selectedLine && selectedLine.openAmount > 0
                ? String(selectedLine.openAmount)
                : ""
            }
            key={sourceLineId}
          />
          {selectedLine ? (
            <span className="muted small">
              Còn mở:{" "}
              {formatMoney(selectedLine.openAmount, selectedLine.currencyCode)}
            </span>
          ) : null}
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
          {submitting || isPending ? "Đang khớp…" : "Thêm chi tiết khớp"}
        </button>
      </div>
    </form>
  );
}
