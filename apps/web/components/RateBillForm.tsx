"use client";

import type { FormEvent, MouseEvent } from "react";
import { useRouter } from "next/navigation";
import { useEffect, useMemo, useState, useTransition } from "react";
import { RatingReadinessBox } from "@/components/RatingReadinessBox";
import type { ApiError, RatingReadiness } from "@/lib/opref-edit";
import { term, type TerminologyMap } from "@/lib/terminology";

type RateOption = {
  rateCardId: string;
  code: string;
  name: string;
  currencyCode: string;
  transportMode: string | null;
  versionId: string;
  versionNo: number;
  selectable: boolean;
  reason: string | null;
};

type RateSide = {
  publicationLabel: string;
  readyLabel: string | null;
  notice: string;
  ambiguous: boolean;
  selectedVersionId: string | null;
  options: RateOption[];
};

type Suggestion = {
  billId: string;
  routeCode: string | null;
  transportMode: string | null;
  buy: RateSide;
  sell: RateSide;
};

type Props = {
  terms: TerminologyMap;
  billId: string;
  editAnchor?: string;
};

function num(raw: string): number | null {
  const text = raw.trim().replace(",", ".");
  if (!text) return null;
  const n = Number(text);
  return Number.isFinite(n) ? n : NaN;
}

function SideBlock({
  title,
  side,
  versionId,
  onVersion,
  readiness,
  readinessError,
  checking,
  editAnchor,
}: {
  title: string;
  side: RateSide;
  versionId: string;
  onVersion: (id: string) => void;
  readiness: RatingReadiness | null;
  readinessError: string | null;
  checking: boolean;
  editAnchor: string;
}) {
  const selectable = side.options.filter((o) => o.selectable);
  const blocked = side.options.filter((o) => !o.selectable);
  return (
    <section className="panel" style={{ margin: 0 }}>
      <h3 className="section-title sm">{title}</h3>
      <p className="muted small">
        {side.publicationLabel}
        {side.readyLabel ? ` · ${side.readyLabel}` : ""}
      </p>
      {side.notice ? (
        <div className="alert alert-warning" role="status">
          {side.notice}
        </div>
      ) : null}
      <div className="field">
        <label htmlFor={`version-${title}`}>Bảng giá / phiên bản đã phát hành</label>
        <select id={`version-${title}`} value={versionId} onChange={(ev) => onVersion(ev.target.value)}>
          <option value="">{selectable.length === 0 ? "— Không chọn được —" : "Chọn bảng giá"}</option>
          {selectable.map((o) => (
            <option key={o.versionId} value={o.versionId}>
              {o.code} — {o.name} · v{o.versionNo} · {o.currencyCode}
            </option>
          ))}
        </select>
      </div>
      {blocked.length > 0 ? (
        <ul className="readiness-list">
          {blocked.map((o) => (
            <li key={o.versionId}>
              <code>{o.code}</code> v{o.versionNo}: {o.reason}
            </li>
          ))}
        </ul>
      ) : null}
      <RatingReadinessBox readiness={readiness} error={readinessError} checking={checking} editAnchor={editAnchor} />
    </section>
  );
}

export function RateBillForm({ terms, billId, editAnchor = "bill-edit" }: Props) {
  const router = useRouter();
  const [suggestion, setSuggestion] = useState<Suggestion | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [buyVersion, setBuyVersion] = useState("");
  const [sellVersion, setSellVersion] = useState("");
  const [buyTouched, setBuyTouched] = useState(false);
  const [sellTouched, setSellTouched] = useState(false);
  const [quantity, setQuantity] = useState("");
  const [weight, setWeight] = useState("");
  const [overrideReason, setOverrideReason] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [isPending, startTransition] = useTransition();
  const [buyReadiness, setBuyReadiness] = useState<RatingReadiness | null>(null);
  const [sellReadiness, setSellReadiness] = useState<RatingReadiness | null>(null);
  const [buyReadinessError, setBuyReadinessError] = useState<string | null>(null);
  const [sellReadinessError, setSellReadinessError] = useState<string | null>(null);
  const [buyChecking, setBuyChecking] = useState(false);
  const [sellChecking, setSellChecking] = useState(false);

  const expected = term(terms, "EXPECTED", "Dự kiến");
  const costLabel = term(terms, "COST", "Chi phí");
  const revenueLabel = term(terms, "REVENUE", "Doanh thu");

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const res = await fetch(`/bff/bills/${billId}/rate-suggestions`, { headers: { Accept: "application/json" } });
        if (res.status === 401) {
          window.location.href = "/login";
          return;
        }
        const payload = (await res.json().catch(() => ({}))) as Suggestion & ApiError;
        if (cancelled) return;
        if (!res.ok) {
          setLoadError(payload.message ?? "Không tải được bảng giá đề xuất.");
          return;
        }
        setSuggestion(payload);
      } catch {
        if (!cancelled) setLoadError("Không kết nối được máy chủ. Thử lại sau.");
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [billId]);

  useEffect(() => {
    if (!suggestion || buyTouched) return;
    setBuyVersion(suggestion.buy.ambiguous ? "" : suggestion.buy.selectedVersionId ?? "");
  }, [suggestion, buyTouched]);

  useEffect(() => {
    if (!suggestion || sellTouched) return;
    setSellVersion(suggestion.sell.ambiguous ? "" : suggestion.sell.selectedVersionId ?? "");
  }, [suggestion, sellTouched]);

  const bodyFor = useMemo(() => {
    return (versionId: string) => {
      const q = num(quantity);
      const w = num(weight);
      return {
        billId,
        rateVersionId: versionId,
        quantity: q === null || Number.isNaN(q) ? null : q,
        weight: w === null || Number.isNaN(w) ? null : w,
        chargeableOverrideReason: overrideReason.trim() || null,
      };
    };
  }, [billId, quantity, weight, overrideReason]);

  useEffect(() => {
    return watchReadiness(bodyFor(buyVersion), setBuyReadiness, setBuyReadinessError, setBuyChecking);
  }, [bodyFor, buyVersion]);

  useEffect(() => {
    return watchReadiness(bodyFor(sellVersion), setSellReadiness, setSellReadinessError, setSellChecking);
  }, [bodyFor, sellVersion]);

  async function rateSide(versionId: string, seed: "cost" | "revenue") {
    const res = await fetch("/bff/ratings", {
      method: "POST",
      headers: { "Content-Type": "application/json", Accept: "application/json" },
      body: JSON.stringify({
        ...bodyFor(versionId),
        supersedesRatingId: null,
        seedExpectedCosts: seed === "cost",
        seedExpectedRevenues: seed === "revenue",
      }),
    });
    if (res.status === 401) {
      window.location.href = "/login";
      return "Phiên đăng nhập đã hết.";
    }
    if (!res.ok) {
      const payload = (await res.json().catch(() => ({}))) as ApiError;
      return payload.message || "Tính giá thất bại.";
    }
    return null;
  }

  async function onSubmit(e: FormEvent<HTMLFormElement> | MouseEvent<HTMLButtonElement>, which: "buy" | "sell" | "both") {
    e.preventDefault();
    setError(null);
    setSuccess(null);
    const q = num(quantity);
    if (q !== null && (Number.isNaN(q) || q <= 0)) {
      setError("Khối tính cước ghi đè phải > 0.");
      return;
    }
    const w = num(weight);
    if (w !== null && (Number.isNaN(w) || w <= 0)) {
      setError("Trọng lượng thực phải > 0. Không dùng 0.");
      return;
    }

    const jobs: { versionId: string; seed: "cost" | "revenue"; label: string }[] = [];
    const notes: string[] = [];
    if (which === "buy" || which === "both") {
      if (buyVersion) jobs.push({ versionId: buyVersion, seed: "cost", label: "Giá mua" });
      else notes.push(suggestion?.buy.notice || "Chưa chọn giá mua.");
    }
    if (which === "sell" || which === "both") {
      if (sellVersion) jobs.push({ versionId: sellVersion, seed: "revenue", label: "Giá bán" });
      else notes.push(suggestion?.sell.notice || "Chưa chọn giá bán.");
    }
    if (jobs.length === 0) {
      setError(notes.join(" ") || "Chưa có chiều nào tính được.");
      return;
    }
    if (which === "buy" && buyReadiness && !buyReadiness.ready) return;
    if (which === "sell" && sellReadiness && !sellReadiness.ready) return;
    if (which === "both") {
      if (buyVersion && buyReadiness && !buyReadiness.ready) return;
      if (sellVersion && sellReadiness && !sellReadiness.ready) return;
    }

    setSubmitting(true);
    try {
      const failures: string[] = [];
      const done: string[] = [];
      for (const job of jobs) {
        const message = await rateSide(job.versionId, job.seed);
        if (message) failures.push(`${job.label}: ${message}`);
        else done.push(job.label);
      }
      if (failures.length > 0) setError(failures.join(" "));
      const parts = [
        done.length > 0 ? `Đã tính ${done.join(" và ")} (${expected.toLowerCase()}).` : "",
        notes.length > 0 ? notes.join(" ") : "",
      ].filter(Boolean);
      if (parts.length > 0) setSuccess(parts.join(" "));
      if (done.length > 0) startTransition(() => router.refresh());
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      setSubmitting(false);
    }
  }

  if (loadError) {
    return (
      <div className="alert alert-error" role="alert">
        {loadError}
      </div>
    );
  }
  if (!suggestion) {
    return <p className="muted">Đang đối chiếu bảng giá với Bill…</p>;
  }

  const busy = submitting || isPending;
  const buyBlocked = Boolean(buyVersion && buyReadiness && !buyReadiness.ready);
  const sellBlocked = Boolean(sellVersion && sellReadiness && !sellReadiness.ready);

  return (
    <form className="receive-form" onSubmit={(e) => onSubmit(e, "both")} noValidate>
      <p className="note">
        Giá mua tạo {costLabel.toLowerCase()} {expected.toLowerCase()}. Giá bán tạo {revenueLabel.toLowerCase()}{" "}
        {expected.toLowerCase()}. Không tạo Thực tế, chứng từ hay công nợ. Thiếu một chiều vẫn tính chiều còn lại.
        Nhiều bảng giá cùng phù hợp thì phải chọn, hệ thống không tự chọn.
      </p>
      {error ? (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      ) : null}
      {success ? (
        <div className="alert alert-info" role="status">
          {success}
        </div>
      ) : null}
      <div className="layout-cols-2">
        <SideBlock
          title="Giá mua — Chi phí dự kiến"
          side={suggestion.buy}
          versionId={buyVersion}
          onVersion={(id) => {
            setBuyTouched(true);
            setBuyVersion(id);
          }}
          readiness={buyReadiness}
          readinessError={buyReadinessError}
          checking={buyChecking}
          editAnchor={editAnchor}
        />
        <SideBlock
          title="Giá bán — Doanh thu dự kiến"
          side={suggestion.sell}
          versionId={sellVersion}
          onVersion={(id) => {
            setSellTouched(true);
            setSellVersion(id);
          }}
          readiness={sellReadiness}
          readinessError={sellReadinessError}
          checking={sellChecking}
          editAnchor={editAnchor}
        />
      </div>
      <details className="stack">
        <summary className="muted">Ghi đè khi tính giá (tuỳ chọn, dùng chung cho lần bấm này)</summary>
        <div className="form-grid">
          <div className="field">
            <label htmlFor="quantity">Khối tính cước ghi đè</label>
            <input id="quantity" inputMode="decimal" placeholder="Theo Bill" value={quantity} onChange={(ev) => setQuantity(ev.target.value)} />
          </div>
          <div className="field">
            <label htmlFor="weight">Trọng lượng thực (kg)</label>
            <input id="weight" inputMode="decimal" placeholder="Theo Bill" value={weight} onChange={(ev) => setWeight(ev.target.value)} />
          </div>
          {quantity.trim() ? (
            <div className="field">
              <label htmlFor="chargeableOverrideReason">Lý do ghi đè</label>
              <input id="chargeableOverrideReason" value={overrideReason} maxLength={500} onChange={(ev) => setOverrideReason(ev.target.value)} />
            </div>
          ) : null}
        </div>
      </details>
      <div className="cta-row">
        <button className={buyVersion && sellVersion ? "btn btn-ghost" : "btn"} type="button" disabled={busy || !buyVersion || buyBlocked} onClick={(e) => onSubmit(e, "buy")}>
          {busy ? "Đang tính…" : "Tính giá mua"}
        </button>
        <button className={buyVersion && sellVersion ? "btn btn-ghost" : "btn"} type="button" disabled={busy || !sellVersion || sellBlocked} onClick={(e) => onSubmit(e, "sell")}>
          {busy ? "Đang tính…" : "Tính giá bán"}
        </button>
        <button className="btn" type="submit" disabled={busy || !buyVersion || !sellVersion || buyBlocked || sellBlocked}>
          {busy ? "Đang tính…" : "Tính giá cả hai"}
        </button>
      </div>
    </form>
  );
}

function watchReadiness(
  body: { rateVersionId: string },
  setReadiness: (value: RatingReadiness | null) => void,
  setError: (value: string | null) => void,
  setChecking: (value: boolean) => void
) {
  if (!body.rateVersionId) {
    setReadiness(null);
    setError(null);
    return undefined;
  }
  let cancelled = false;
  const timer = window.setTimeout(async () => {
    setChecking(true);
    setError(null);
    try {
      const res = await fetch("/bff/ratings/readiness", {
        method: "POST",
        headers: { "Content-Type": "application/json", Accept: "application/json" },
        body: JSON.stringify(body),
      });
      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }
      const payload = (await res.json().catch(() => ({}))) as RatingReadiness & ApiError;
      if (cancelled) return;
      if (!res.ok) {
        setReadiness(null);
        setError(payload.message ?? "Không kiểm tra được dữ liệu tính giá.");
        return;
      }
      setReadiness(payload);
    } catch {
      if (!cancelled) setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      if (!cancelled) setChecking(false);
    }
  }, 350);
  return () => {
    cancelled = true;
    window.clearTimeout(timer);
  };
}
