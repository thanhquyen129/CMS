"use client";

import type { FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useEffect, useMemo, useState, useTransition } from "react";
import type { RateCard, RateVersion } from "@/lib/rate-cards";
import {
  isPublishedVersion,
  partyTypeLabel,
  tariffCommodityOptions,
} from "@/lib/rate-cards";
import { term, type TerminologyMap } from "@/lib/terminology";
import { RatingReadinessBox } from "@/components/RatingReadinessBox";
import type { ApiError, RatingReadiness } from "@/lib/opref-edit";

type CardWithVersions = {
  card: RateCard;
  versions: RateVersion[];
};

type Props = {
  terms: TerminologyMap;
  billId: string;
  cardsWithVersions: CardWithVersions[];
  /** Anchor of the Bill edit section on the same page. */
  editAnchor?: string;
};

function num(raw: string): number | null {
  const text = raw.trim().replace(",", ".");
  if (!text) return null;
  const n = Number(text);
  return Number.isFinite(n) ? n : NaN;
}

export function RateBillForm({ terms, billId, cardsWithVersions, editAnchor = "bill-edit" }: Props) {
  const router = useRouter();
  const cardsWithPublished = cardsWithVersions.filter((c) =>
    c.versions.some((v) => isPublishedVersion(v.status))
  );

  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [isPending, startTransition] = useTransition();
  const [cardId, setCardId] = useState(cardsWithPublished[0]?.card.id ?? "");
  const [versionId, setVersionId] = useState("");
  const [quantity, setQuantity] = useState("");
  const [overrideReason, setOverrideReason] = useState("");
  const [commodityCode, setCommodityCode] = useState("");
  const [destinationCode, setDestinationCode] = useState("");
  const [weight, setWeight] = useState("");
  const [serviceTypeCode, setServiceTypeCode] = useState("");
  const [partyTypeCode, setPartyTypeCode] = useState("");
  const [routeCode, setRouteCode] = useState("");
  const [baseAmount, setBaseAmount] = useState("");
  const [readiness, setReadiness] = useState<RatingReadiness | null>(null);
  const [readinessError, setReadinessError] = useState<string | null>(null);
  const [checking, setChecking] = useState(false);

  const expected = term(terms, "EXPECTED", "Dự kiến");
  const costLabel = term(terms, "COST", "Chi phí");

  const publishedVersions = useMemo(() => {
    const entry = cardsWithPublished.find((c) => c.card.id === cardId);
    if (!entry) return [];
    return entry.versions.filter((v) => isPublishedVersion(v.status));
  }, [cardId, cardsWithPublished]);

  const selectedCard =
    cardsWithPublished.find((c) => c.card.id === cardId) ?? cardsWithPublished[0];
  const effectiveVersionId = versionId || publishedVersions[0]?.id || "";
  const mode = selectedCard?.card.transportMode?.toLowerCase();

  const body = useMemo(() => {
    const q = num(quantity);
    const w = num(weight);
    return {
      billId,
      rateVersionId: effectiveVersionId,
      quantity: q === null || Number.isNaN(q) ? null : q,
      weight: w === null || Number.isNaN(w) ? null : w,
      commodityCode: commodityCode || null,
      destinationCode: destinationCode || null,
      serviceTypeCode: serviceTypeCode.trim() || null,
      partyTypeCode: partyTypeCode.trim() || null,
      routeCode: routeCode.trim() || null,
      chargeableOverrideReason: overrideReason.trim() || null,
    };
  }, [billId, effectiveVersionId, quantity, weight, commodityCode, destinationCode, serviceTypeCode, partyTypeCode, routeCode, overrideReason]);

  useEffect(() => {
    if (!body.rateVersionId) {
      setReadiness(null);
      return;
    }
    let cancelled = false;
    const timer = window.setTimeout(async () => {
      setChecking(true);
      setReadinessError(null);
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
          setReadinessError(payload.message ?? "Không kiểm tra được dữ liệu tính giá.");
          return;
        }
        setReadiness(payload);
      } catch {
        if (!cancelled) setReadinessError("Không kết nối được máy chủ. Thử lại sau.");
      } finally {
        if (!cancelled) setChecking(false);
      }
    }, 350);
    return () => {
      cancelled = true;
      window.clearTimeout(timer);
    };
  }, [body]);

  if (cardsWithPublished.length === 0) {
    return (
      <div className="empty-state" role="status">
        Chưa có phiên bản bảng giá đã phát hành. Tạo bảng giá → quy tắc → phát
        hành trước khi tính giá.
      </div>
    );
  }

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
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
      setError("Trọng lượng thực phải > 0.");
      return;
    }
    if (!body.rateVersionId) {
      setError("Chọn phiên bản đã phát hành.");
      return;
    }

    const base = num(baseAmount);
    if (base !== null && Number.isNaN(base)) {
      setError("Số cơ sở không hợp lệ.");
      return;
    }

    const fd = new FormData(e.currentTarget);
    const seedExpectedCosts = fd.get("seedExpectedCosts") === "on";
    setSubmitting(true);
    try {
      const res = await fetch("/bff/ratings", {
        method: "POST",
        headers: { "Content-Type": "application/json", Accept: "application/json" },
        body: JSON.stringify({ ...body, baseAmount: base, supersedesRatingId: null, seedExpectedCosts }),
      });
      if (res.status === 401) {
        window.location.href = "/login";
        return;
      }
      if (!res.ok) {
        const payload = (await res.json().catch(() => ({}))) as ApiError;
        if (payload.code === "rating_not_ready" && payload.missing && readiness) {
          setReadiness({ ...readiness, ready: false, missing: payload.missing });
        }
        setError(
          payload.message ||
            (res.status === 403 ? "Bạn không có quyền tính giá." : "Tính giá thất bại.")
        );
        return;
      }
      setSuccess(
        seedExpectedCosts
          ? `Đã tính giá và seed ${costLabel.toLowerCase()} ${expected.toLowerCase()}.`
          : "Đã tính giá. Có thể seed chi phí Dự kiến từ lịch sử bên dưới."
      );
      startTransition(() => router.refresh());
    } catch {
      setError("Không kết nối được máy chủ. Thử lại sau.");
    } finally {
      setSubmitting(false);
    }
  }

  const busy = submitting || isPending;
  const blocked = readiness !== null && !readiness.ready;
  const hasQuantity = quantity.trim() !== "";

  return (
    <form className="receive-form" onSubmit={onSubmit} noValidate>
      <p className="note">
        Dữ liệu tính giá lấy từ Bill (phương thức, tuyến, loại hàng, trọng lượng, thể tích, Trọng lượng tính cước).
        Thiếu gì hệ thống sẽ liệt kê bên dưới — bổ sung trên Bill rồi tính giá. Chỉ nhập các ô bên dưới khi cần
        khác với Bill.
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

      <div className="form-grid">
        <div className="field">
          <label htmlFor="rateCardId">Bảng giá</label>
          <select
            id="rateCardId"
            value={selectedCard?.card.id ?? ""}
            onChange={(ev) => {
              setCardId(ev.target.value);
              setVersionId("");
              setCommodityCode("");
            }}
          >
            {cardsWithPublished.map(({ card }) => (
              <option key={card.id} value={card.id}>
                {card.code} — {card.name} ({partyTypeLabel(card.partyType)})
              </option>
            ))}
          </select>
        </div>
        <div className="field">
          <label htmlFor="rateVersionId">Phiên bản đã phát hành</label>
          <select
            id="rateVersionId"
            value={effectiveVersionId}
            onChange={(ev) => setVersionId(ev.target.value)}
          >
            {publishedVersions.length === 0 ? (
              <option value="">— Không có —</option>
            ) : (
              publishedVersions.map((v) => (
                <option key={v.id} value={v.id}>
                  v{v.versionNo}
                  {v.note ? ` — ${v.note}` : ""}
                </option>
              ))
            )}
          </select>
        </div>
        <div className="field">
          <label htmlFor="commodityCode">Loại hàng</label>
          <select id="commodityCode" value={commodityCode} onChange={(ev) => setCommodityCode(ev.target.value)}>
            <option value="">Theo Bill</option>
            {tariffCommodityOptions
              .filter((opt) => {
                if (mode !== "air" && mode !== "sea") return true;
                return (opt.modes as readonly string[]).includes(mode);
              })
              .map((opt) => (
                <option key={opt.value} value={opt.value}>
                  {opt.label}
                </option>
              ))}
          </select>
        </div>
        <div className="field">
          <label htmlFor="destinationCode">Điểm đến (phụ phí vùng)</label>
          <select id="destinationCode" value={destinationCode} onChange={(ev) => setDestinationCode(ev.target.value)}>
            <option value="">Theo Bill</option>
            <option value="SBH">Sabah (SBH)</option>
            <option value="SWK">Sarawak (SWK)</option>
          </select>
        </div>
      </div>

      <details className="stack">
        <summary className="muted">Ghi đè khi tính giá (tuỳ chọn)</summary>
        <div className="form-grid">
          <div className="field">
            <label htmlFor="quantity">{mode === "sea" ? "Khối tính cước ghi đè (W/M)" : "Trọng lượng tính cước ghi đè (kg)"}</label>
            <input
              id="quantity"
              inputMode="decimal"
              placeholder="Theo Bill"
              value={quantity}
              onChange={(ev) => setQuantity(ev.target.value)}
            />
          </div>
          {hasQuantity ? (
            <div className="field">
              <label htmlFor="chargeableOverrideReason">Lý do ghi đè</label>
              <input
                id="chargeableOverrideReason"
                value={overrideReason}
                maxLength={500}
                placeholder="Bắt buộc khi Bill đã có Trọng lượng tính cước"
                onChange={(ev) => setOverrideReason(ev.target.value)}
              />
            </div>
          ) : null}
          <div className="field">
            <label htmlFor="weight">Trọng lượng thực (kg)</label>
            <input id="weight" inputMode="decimal" placeholder="Theo Bill" value={weight} onChange={(ev) => setWeight(ev.target.value)} />
          </div>
          <div className="field">
            <label htmlFor="serviceTypeCode">Loại dịch vụ</label>
            <input id="serviceTypeCode" placeholder="Theo Bill" value={serviceTypeCode} onChange={(ev) => setServiceTypeCode(ev.target.value)} />
          </div>
          <div className="field">
            <label htmlFor="partyTypeCode">Mã loại đối tác</label>
            <input id="partyTypeCode" placeholder="Theo bảng giá" value={partyTypeCode} onChange={(ev) => setPartyTypeCode(ev.target.value)} />
          </div>
          <div className="field">
            <label htmlFor="routeCode">Mã tuyến</label>
            <input id="routeCode" placeholder="Theo Bill" value={routeCode} onChange={(ev) => setRouteCode(ev.target.value)} />
          </div>
          <div className="field">
            <label htmlFor="baseAmount">Số cơ sở (cho % — tuỳ chọn)</label>
            <input id="baseAmount" inputMode="decimal" value={baseAmount} onChange={(ev) => setBaseAmount(ev.target.value)} />
          </div>
        </div>
      </details>

      <RatingReadinessBox
        readiness={readiness}
        error={readinessError}
        checking={checking}
        editAnchor={editAnchor}
      />

      <div className="form-grid">
        <div className="field field-span">
          <label className="checkbox-label">
            <input type="checkbox" name="seedExpectedCosts" defaultChecked /> Seed{" "}
            {costLabel.toLowerCase()} {expected.toLowerCase()} ngay
          </label>
        </div>
      </div>

      <div className="cta-row">
        <button
          className="btn"
          type="submit"
          disabled={busy || publishedVersions.length === 0 || blocked}
          title={blocked ? "Bổ sung dữ liệu còn thiếu trước khi tính giá." : undefined}
        >
          {busy ? "Đang tính…" : "Tính giá"}
        </button>
      </div>
    </form>
  );
}
