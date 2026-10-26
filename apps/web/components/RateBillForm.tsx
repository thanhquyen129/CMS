"use client";

import type { FormEvent, MouseEvent, ReactNode } from "react";
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
  reasonCategory?: string | null;
  detailedReason?: string | null;
};

type RateSide = {
  publicationLabel: string;
  readyLabel: string | null;
  notice: string;
  ambiguous: boolean;
  selectedVersionId: string | null;
  options: RateOption[];
};

type LegOption = {
  id: string;
  legNo: string;
  shipmentId: string;
  shipmentNo: string;
  originCode: string | null;
  destinationCode: string | null;
  transportMode: string | null;
  routeCode: string | null;
  linkedBillCount: number;
};

type VendorOption = { id: string; code: string; name: string };

type Suggestion = {
  billId: string;
  routeCode: string | null;
  transportMode: string | null;
  buy: RateSide;
  sell: RateSide;
  contextType?: string;
  legId?: string | null;
  vendorPartyId?: string | null;
  legs?: LegOption[];
  vendors?: VendorOption[];
  activeRatingId?: string | null;
  activeRatingQuantity?: number | null;
  activeRatingWeight?: number | null;
  rateDateFormatted?: string | null;
  rateDateSource?: string | null;
  rateDateSourceLabel?: string | null;
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

function changed(typed: number | null, active: number | null | undefined): boolean {
  if (typed === null || Number.isNaN(typed) || active == null) return false;
  return Math.abs(typed - active) > 0.0001;
}

function SideBlock({
  title,
  sideId,
  side,
  versionId,
  onVersion,
  quantity,
  weight,
  reason,
  onQuantity,
  onWeight,
  onReason,
  readiness,
  readinessError,
  checking,
  editAnchor,
  extra,
}: {
  title: string;
  sideId: string;
  side: RateSide;
  versionId: string;
  onVersion: (id: string) => void;
  quantity: string;
  weight: string;
  reason: string;
  onQuantity: (value: string) => void;
  onWeight: (value: string) => void;
  onReason: (value: string) => void;
  readiness: RatingReadiness | null;
  readinessError: string | null;
  checking: boolean;
  editAnchor: string;
  extra?: ReactNode;
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
      {extra}
      <div className="field">
        <label htmlFor={`version-${sideId}`}>Bảng giá / phiên bản đủ điều kiện áp dụng</label>
        <select id={`version-${sideId}`} value={versionId} onChange={(ev) => onVersion(ev.target.value)}>
          <option value="">{selectable.length === 0 ? "— Không có phiên bản đủ điều kiện —" : "Chọn bảng giá (Đủ điều kiện)"}</option>
          {selectable.map((o) => (
            <option key={o.versionId} value={o.versionId}>
              {o.code} — {o.name} · v{o.versionNo} · {o.currencyCode} (Đủ điều kiện áp dụng)
            </option>
          ))}
        </select>
      </div>
      {blocked.length > 0 ? (
        <div style={{ marginTop: "8px", marginBottom: "12px" }}>
          <p className="muted small" style={{ fontWeight: 600 }}>Phiên bản không đủ điều kiện / Chỉ tham khảo:</p>
          <ul className="readiness-list">
            {blocked.map((o) => (
              <li key={o.versionId}>
                <strong><code>{o.code}</code> v{o.versionNo}</strong>:{" "}
                <span style={{ color: "#b45309" }}>{o.detailedReason || o.reason}</span>
                <span className="badge badge-subtle" style={{ marginLeft: "8px" }}>Chỉ tham khảo</span>
              </li>
            ))}
          </ul>
        </div>
      ) : null}
      <div className="form-grid">
        <div className="field">
          <label htmlFor={`qty-${sideId}`}>Khối tính cước ghi đè</label>
          <input id={`qty-${sideId}`} inputMode="decimal" placeholder="Theo Bill" value={quantity} onChange={(ev) => onQuantity(ev.target.value)} />
        </div>
        <div className="field">
          <label htmlFor={`weight-${sideId}`}>Trọng lượng thực (kg)</label>
          <input id={`weight-${sideId}`} inputMode="decimal" placeholder="Theo Bill" value={weight} onChange={(ev) => onWeight(ev.target.value)} />
        </div>
        {quantity.trim() ? (
          <div className="field">
            <label htmlFor={`reason-${sideId}`}>Lý do ghi đè</label>
            <input id={`reason-${sideId}`} value={reason} maxLength={500} onChange={(ev) => onReason(ev.target.value)} />
          </div>
        ) : null}
      </div>
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
  const [buyQuantity, setBuyQuantity] = useState("");
  const [sellQuantity, setSellQuantity] = useState("");
  const [buyWeight, setBuyWeight] = useState("");
  const [sellWeight, setSellWeight] = useState("");
  const [buyReason, setBuyReason] = useState("");
  const [sellReason, setSellReason] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [isPending, startTransition] = useTransition();
  const [buyReadiness, setBuyReadiness] = useState<RatingReadiness | null>(null);
  const [sellReadiness, setSellReadiness] = useState<RatingReadiness | null>(null);
  const [buyReadinessError, setBuyReadinessError] = useState<string | null>(null);
  const [sellReadinessError, setSellReadinessError] = useState<string | null>(null);
  const [buyChecking, setBuyChecking] = useState(false);
  const [sellChecking, setSellChecking] = useState(false);
  const [buyContext, setBuyContext] = useState<"bill" | "leg">("bill");
  const [legId, setLegId] = useState("");
  const [vendorId, setVendorId] = useState("");
  const [vendorTouched, setVendorTouched] = useState(false);
  const [replaceExisting, setReplaceExisting] = useState(false);

  const expected = term(terms, "EXPECTED", "Dự kiến");
  const costLabel = term(terms, "COST", "Chi phí");
  const revenueLabel = term(terms, "REVENUE", "Doanh thu");

  useEffect(() => {
    let cancelled = false;
    const params = new URLSearchParams();
    if (buyContext === "leg") {
      params.set("contextType", "leg");
      if (legId) params.set("legId", legId);
      if (vendorId) params.set("vendorPartyId", vendorId);
      if (buyVersion) params.set("rateVersionId", buyVersion);
    }
    const query = params.toString();
    (async () => {
      try {
        const res = await fetch(`/bff/bills/${billId}/rate-suggestions${query ? `?${query}` : ""}`, { headers: { Accept: "application/json" } });
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
  }, [billId, buyContext, legId, vendorId, buyVersion]);

  useEffect(() => {
    if (!suggestion || buyTouched) return;
    setBuyVersion(suggestion.buy.ambiguous ? "" : suggestion.buy.selectedVersionId ?? "");
  }, [suggestion, buyTouched]);

  useEffect(() => {
    if (!suggestion || buyContext !== "leg" || vendorTouched) return;
    setVendorId(suggestion.vendorPartyId ?? "");
  }, [suggestion, buyContext, vendorTouched]);

  useEffect(() => {
    if (!suggestion || sellTouched) return;
    setSellVersion(suggestion.sell.ambiguous ? "" : suggestion.sell.selectedVersionId ?? "");
  }, [suggestion, sellTouched]);

  const bodyFor = useMemo(() => {
    return (side: "buy" | "sell", versionId: string) => {
      const q = num(side === "buy" ? buyQuantity : sellQuantity);
      const w = num(side === "buy" ? buyWeight : sellWeight);
      const reason = side === "buy" ? buyReason : sellReason;
      const activeId = suggestion?.activeRatingId ?? null;
      const quantityChanged = changed(q, suggestion?.activeRatingQuantity);
      const weightChanged = changed(w, suggestion?.activeRatingWeight);
      const supersedesRatingId =
        side === "buy" && buyContext === "leg" && activeId && (replaceExisting || quantityChanged || weightChanged)
          ? activeId
          : null;
      return {
        billId,
        rateVersionId: versionId,
        quantity: q === null || Number.isNaN(q) ? null : q,
        weight: w === null || Number.isNaN(w) ? null : w,
        chargeableOverrideReason: reason.trim() || null,
        ...(side === "buy"
          ? buyContext === "leg"
            ? {
                contextType: "leg",
                transportLegId: legId || null,
                vendorPartyId: vendorId || null,
                supersedesRatingId,
              }
            : { contextType: "bill" }
          : {}),
      };
    };
  }, [billId, buyQuantity, sellQuantity, buyWeight, sellWeight, buyReason, sellReason, buyContext, legId, vendorId, replaceExisting, suggestion]);

  useEffect(() => {
    return watchReadiness(bodyFor("buy", buyVersion), setBuyReadiness, setBuyReadinessError, setBuyChecking);
  }, [bodyFor, buyVersion]);

  useEffect(() => {
    return watchReadiness(bodyFor("sell", sellVersion), setSellReadiness, setSellReadinessError, setSellChecking);
  }, [bodyFor, sellVersion]);

  async function rateSide(side: "buy" | "sell", versionId: string, seed: "cost" | "revenue") {
    const res = await fetch("/bff/ratings", {
      method: "POST",
      headers: { "Content-Type": "application/json", Accept: "application/json" },
      body: JSON.stringify({
        ...bodyFor(side, versionId),
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
    const sides: Array<"buy" | "sell"> = which === "both" ? ["buy", "sell"] : [which];
    if ((which === "buy" || which === "both") && buyContext === "leg") {
      if (!legId) {
        setError("Chọn chặng liên kết với Bill.");
        return;
      }
      if (!vendorId) {
        setError("Chọn vendor cho giá mua theo chặng.");
        return;
      }
    }
    for (const side of sides) {
      const q = num(side === "buy" ? buyQuantity : sellQuantity);
      const w = num(side === "buy" ? buyWeight : sellWeight);
      if (q !== null && (Number.isNaN(q) || q <= 0)) {
        setError(side === "buy" ? "Khối tính cước giá mua phải > 0." : "Khối tính cước giá bán phải > 0.");
        return;
      }
      if (w !== null && (Number.isNaN(w) || w <= 0)) {
        setError(side === "buy" ? "Trọng lượng giá mua phải > 0. Không dùng 0." : "Trọng lượng giá bán phải > 0. Không dùng 0.");
        return;
      }
    }

    const jobs: { side: "buy" | "sell"; versionId: string; seed: "cost" | "revenue"; label: string }[] = [];
    const notes: string[] = [];
    if (which === "buy" || which === "both") {
      if (buyVersion) jobs.push({ side: "buy", versionId: buyVersion, seed: "cost", label: "Giá mua" });
      else notes.push(suggestion?.buy.notice || "Chưa chọn giá mua.");
    }
    if (which === "sell" || which === "both") {
      if (sellVersion) jobs.push({ side: "sell", versionId: sellVersion, seed: "revenue", label: "Giá bán" });
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
        const message = await rateSide(job.side, job.versionId, job.seed);
        if (message) failures.push(`${job.label}: ${message}`);
        else done.push(job.label);
      }
      if (failures.length > 0) setError(failures.join(" "));
      const sharedLeg = buyContext === "leg" && (suggestion?.legs?.find((l) => l.id === legId)?.linkedBillCount ?? 0) >= 2;
      const parts = [
        done.length > 0 ? `Đã tính ${done.join(" và ")} (${expected.toLowerCase()}).` : "",
        done.includes("Giá mua") && sharedLeg
          ? "Chi phí nguồn dùng chung nhiều Bill. Mỗi Bill chỉ nhận phần sau khi phân bổ."
          : "",
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
  const buyContextBlocked = buyContext === "leg" && (!legId || !vendorId);

  return (
    <form className="receive-form" onSubmit={(e) => onSubmit(e, "both")} noValidate>
      <p className="note">
        Giá mua tạo {costLabel.toLowerCase()} {expected.toLowerCase()}. Giá bán tạo {revenueLabel.toLowerCase()}{" "}
        {expected.toLowerCase()}. Không tạo Thực tế, chứng từ hay công nợ. Mỗi chiều có khối lượng riêng.
        Nút từng chiều không bị khóa khi chiều kia chưa có bảng giá. Tính cả hai chỉ chạy khi cả hai đã chọn.
        Nhiều bảng giá cùng phù hợp thì phải chọn, hệ thống không tự chọn. Giá mua có thể tính theo Bill hoặc theo chặng liên kết. Giá bán giữ theo Bill.
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
      {suggestion.rateDateFormatted ? (
        <div className="alert alert-info" role="status" style={{ marginBottom: "16px" }}>
          <strong>Ngày áp dụng giá:</strong> {suggestion.rateDateFormatted} &nbsp;·&nbsp; <strong>Nguồn xác định:</strong> {suggestion.rateDateSourceLabel ?? "Theo Bill"}
        </div>
      ) : null}
      <div className="layout-cols-2">
        <SideBlock
          title="Giá mua — Chi phí dự kiến"
          sideId="buy"
          side={suggestion.buy}
          versionId={buyVersion}
          onVersion={(id) => {
            setBuyTouched(true);
            setBuyVersion(id);
          }}
          quantity={buyQuantity}
          weight={buyWeight}
          reason={buyReason}
          onQuantity={setBuyQuantity}
          onWeight={setBuyWeight}
          onReason={setBuyReason}
          readiness={buyReadiness}
          readinessError={buyReadinessError}
          checking={buyChecking}
          editAnchor={editAnchor}
          extra={<BuyContextFields
            context={buyContext}
            legs={suggestion.legs ?? []}
            legId={legId}
            vendors={suggestion.vendors ?? []}
            vendorId={vendorId}
            onContext={(next) => {
              setBuyContext(next);
              setBuyTouched(false);
              setBuyVersion("");
              setVendorTouched(false);
              setReplaceExisting(false);
              if (next === "bill") {
                setLegId("");
                setVendorId("");
              }
            }}
            onLeg={(id) => {
              setLegId(id);
              setVendorId("");
              setVendorTouched(false);
              setBuyTouched(false);
              setBuyVersion("");
              setReplaceExisting(false);
            }}
            onVendor={(id) => {
              setVendorTouched(true);
              setVendorId(id);
              setBuyTouched(false);
              setBuyVersion("");
              setReplaceExisting(false);
            }}
            activeRatingId={suggestion.activeRatingId ?? null}
            replaceExisting={replaceExisting}
            onReplace={setReplaceExisting}
          />}
        />
        <SideBlock
          title="Giá bán — Doanh thu dự kiến"
          sideId="sell"
          side={suggestion.sell}
          versionId={sellVersion}
          onVersion={(id) => {
            setSellTouched(true);
            setSellVersion(id);
          }}
          quantity={sellQuantity}
          weight={sellWeight}
          reason={sellReason}
          onQuantity={setSellQuantity}
          onWeight={setSellWeight}
          onReason={setSellReason}
          readiness={sellReadiness}
          readinessError={sellReadinessError}
          checking={sellChecking}
          editAnchor={editAnchor}
        />
      </div>
      <div className="cta-row">
        <button className={buyVersion && sellVersion ? "btn btn-ghost" : "btn"} type="button" disabled={busy || !buyVersion || buyBlocked || buyContextBlocked} onClick={(e) => onSubmit(e, "buy")}>
          {busy ? "Đang tính…" : "Tính giá mua"}
        </button>
        <button className={buyVersion && sellVersion ? "btn btn-ghost" : "btn"} type="button" disabled={busy || !sellVersion || sellBlocked} onClick={(e) => onSubmit(e, "sell")}>
          {busy ? "Đang tính…" : "Tính giá bán"}
        </button>
        <button className="btn" type="submit" disabled={busy || !buyVersion || !sellVersion || buyBlocked || sellBlocked || buyContextBlocked}>
          {busy ? "Đang tính…" : "Tính giá cả hai"}
        </button>
      </div>
    </form>
  );
}

function legLabel(leg: LegOption): string {
  const route = [leg.originCode, leg.destinationCode].filter(Boolean).join(" → ");
  return `${leg.legNo} · ${leg.shipmentNo || "Lô hàng"} · ${route || "chưa rõ tuyến"} · ${leg.transportMode || "chưa rõ phương thức"}`;
}

function BuyContextFields({
  context,
  legs,
  legId,
  vendors,
  vendorId,
  onContext,
  onLeg,
  onVendor,
  activeRatingId,
  replaceExisting,
  onReplace,
}: {
  context: "bill" | "leg";
  legs: LegOption[];
  legId: string;
  vendors: VendorOption[];
  vendorId: string;
  onContext: (value: "bill" | "leg") => void;
  onLeg: (id: string) => void;
  onVendor: (id: string) => void;
  activeRatingId: string | null;
  replaceExisting: boolean;
  onReplace: (value: boolean) => void;
}) {
  return (
    <>
      <fieldset className="field">
        <legend>Ngữ cảnh giá mua</legend>
        <div className="create-checks">
          <label className="checkbox-field">
            <input type="radio" name="buy-context" checked={context === "bill"} onChange={() => onContext("bill")} />
            Theo Bill
          </label>
          <label className="checkbox-field">
            <input type="radio" name="buy-context" checked={context === "leg"} onChange={() => onContext("leg")} />
            Theo Chặng
          </label>
        </div>
      </fieldset>
      {context === "leg" ? (
        <>
          <div className="field">
            <label htmlFor="buy-leg">Chặng liên kết Bill</label>
            <select id="buy-leg" value={legId} onChange={(ev) => onLeg(ev.target.value)}>
              <option value="">{legs.length === 0 ? "Bill chưa có chặng liên kết" : "Chọn chặng"}</option>
              {legs.map((leg) => (
                <option key={leg.id} value={leg.id}>
                  {legLabel(leg)}
                </option>
              ))}
            </select>
          </div>
          <div className="field">
            <label htmlFor="buy-vendor">Vendor</label>
            <select id="buy-vendor" value={vendorId} disabled={!legId} onChange={(ev) => onVendor(ev.target.value)}>
              <option value="">{vendors.length === 0 ? "Chưa có vendor phù hợp" : "Chọn vendor"}</option>
              {vendors.map((vendor) => (
                <option key={vendor.id} value={vendor.id}>
                  {vendor.code} — {vendor.name}
                </option>
              ))}
            </select>
          </div>
          {activeRatingId ? (
            <label className="checkbox-field">
              <input type="checkbox" checked={replaceExisting} onChange={(ev) => onReplace(ev.target.checked)} />
              Tính lại và thay thế lần tính hiện có. Đổi số lượng hoặc trọng lượng cũng thay lần tính đó.
            </label>
          ) : null}
        </>
      ) : null}
    </>
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
