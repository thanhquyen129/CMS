"use client";

import type { FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { term, type TerminologyMap } from "@/lib/terminology";

type Props = {
  terms: TerminologyMap;
  defaultScopeType?: string;
  defaultScopeId?: string;
};

export function StartFinancialCloseForm({
  terms,
  defaultScopeType = "period",
  defaultScopeId,
}: Props) {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [isPending, startTransition] = useTransition();
  const [scopeType, setScopeType] = useState(
    defaultScopeType === "bill" ? "bill" : "period"
  );

  const closeLabel = term(terms, "FINANCIAL_CLOSE", "Chốt tài chính");
  const billLabel = term(terms, "BILL", "Bill");
  const periodLock = term(terms, "PERIOD_LOCK", "Khóa kỳ");

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setError(null);
    setSubmitting(true);

    const fd = new FormData(e.currentTarget);
    const scope = String(fd.get("scopeType") ?? "period").trim();
    const scopeIdRaw = String(fd.get("scopeId") ?? "").trim();
    if (scope === "bill" && !scopeIdRaw) {
      setError(`Chốt theo ${billLabel} cần UUID ${billLabel}.`);
      setSubmitting(false);
      return;
    }

    const body = {
      scopeType: scope,
      scopeId: scope === "bill" ? scopeIdRaw : null,
      periodFrom: String(fd.get("periodFrom") ?? "").trim() || null,
      periodTo: String(fd.get("periodTo") ?? "").trim() || null,
      policyVersion: String(fd.get("policyVersion") ?? "controlled").trim(),
      baseCurrency: String(fd.get("baseCurrency") ?? "VND")
        .trim()
        .toUpperCase(),
      notes: String(fd.get("notes") ?? "").trim() || null,
      supersedesCloseId: null,
    };

    try {
      const res = await fetch("/bff/financial-closes", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Accept: "application/json",
        },
        body: JSON.stringify(body),
      });

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
              ? "Không mở lần chốt (xung đột / eligibility)."
              : `Mở ${closeLabel.toLowerCase()} thất bại.`)
        );
        return;
      }

      const created = (await res.json().catch(() => ({}))) as { id?: string };
      if (created.id) {
        startTransition(() =>
          router.push(`/financial-closes/${created.id}`)
        );
      } else {
        startTransition(() => router.push("/financial-closes"));
      }
      router.refresh();
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      setSubmitting(false);
    }
  }

  const busy = submitting || isPending;

  return (
    <form className="receive-form" onSubmit={onSubmit} noValidate>
      <p className="note">
        Mở lần {closeLabel.toLowerCase()} (trạng thái đang mở). Bản chốt bất
        biến và {periodLock.toLowerCase()} chỉ sau khi tạo snapshot.
      </p>

      {error ? (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      ) : null}

      <div className="form-grid">
        <div className="field">
          <label htmlFor="scopeType">Phạm vi</label>
          <select
            id="scopeType"
            name="scopeType"
            required
            disabled={busy}
            value={scopeType}
            onChange={(e) => setScopeType(e.target.value)}
          >
            <option value="period">Kỳ</option>
            <option value="bill">{billLabel}</option>
          </select>
        </div>
        {scopeType === "bill" ? (
          <div className="field">
            <label htmlFor="scopeId">{billLabel} (UUID)</label>
            <input
              id="scopeId"
              name="scopeId"
              required
              defaultValue={defaultScopeId ?? ""}
              disabled={busy}
              autoComplete="off"
            />
          </div>
        ) : (
          <>
            <div className="field">
              <label htmlFor="periodFrom">Từ ngày</label>
              <input
                id="periodFrom"
                name="periodFrom"
                type="date"
                disabled={busy}
              />
            </div>
            <div className="field">
              <label htmlFor="periodTo">Đến ngày</label>
              <input
                id="periodTo"
                name="periodTo"
                type="date"
                disabled={busy}
              />
            </div>
          </>
        )}
        <div className="field">
          <label htmlFor="policyVersion">Chính sách</label>
          <select
            id="policyVersion"
            name="policyVersion"
            disabled={busy}
            defaultValue="controlled"
          >
            <option value="controlled">Controlled</option>
            <option value="strict">Strict</option>
          </select>
        </div>
        <div className="field">
          <label htmlFor="baseCurrency">Tiền tệ gốc</label>
          <input
            id="baseCurrency"
            name="baseCurrency"
            defaultValue="VND"
            maxLength={3}
            required
            disabled={busy}
          />
        </div>
        <div className="field field-span">
          <label htmlFor="notes">Ghi chú</label>
          <input id="notes" name="notes" maxLength={2048} disabled={busy} />
        </div>
      </div>

      <div className="cta-row">
        <button type="submit" className="btn" disabled={busy}>
          {busy ? "Đang mở…" : `Mở ${closeLabel.toLowerCase()}`}
        </button>
      </div>
    </form>
  );
}
